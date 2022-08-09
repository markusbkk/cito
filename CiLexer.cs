// CiLexer.cs - Ci lexer
//
// Copyright (C) 2011-2022  Piotr Fusik
//
// This file is part of CiTo, see https://github.com/pfusik/cito
//
// CiTo is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// CiTo is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with CiTo.  If not, see http://www.gnu.org/licenses/

using System;
using System.Collections.Generic;
using System.Text;

namespace Foxoft.Ci
{

public enum CiToken
{
	EndOfFile,
	Id,
	LiteralLong,
	LiteralDouble,
	LiteralChar,
	LiteralString,
	InterpolatedString,
	Semicolon,
	Dot,
	Comma,
	LeftParenthesis,
	RightParenthesis,
	LeftBracket,
	RightBracket,
	LeftBrace,
	RightBrace,
	Plus,
	Minus,
	Asterisk,
	Slash,
	Mod,
	And,
	Or,
	Xor,
	Tilde,
	ShiftLeft,
	ShiftRight,
	Equal,
	NotEqual,
	Less,
	LessOrEqual,
	Greater,
	GreaterOrEqual,
	RightAngle,
	CondAnd,
	CondOr,
	ExclamationMark,
	Hash,
	Assign,
	AddAssign,
	SubAssign,
	MulAssign,
	DivAssign,
	ModAssign,
	AndAssign,
	OrAssign,
	XorAssign,
	ShiftLeftAssign,
	ShiftRightAssign,
	Increment,
	Decrement,
	QuestionMark,
	Colon,
	FatArrow,
	Range,
	DocComment,
	Abstract,
	Assert,
	Break,
	Case,
	Class,
	Const,
	Continue,
	Default,
	Do,
	Else,
	Enum,
	False,
	For,
	Foreach,
	If,
	In,
	Internal,
	Is,
	Lock,
	Native,
	New,
	Null,
	Override,
	Protected,
	Public,
	Resource,
	Return,
	Sealed,
	Static,
	Switch,
	Throw,
	Throws,
	True,
	Virtual,
	Void,
	While,
	EndOfLine,
	PreIf,
	PreElIf,
	PreElse,
	PreEndIf
}

public enum CiDocToken
{
	EndOfFile,
	Char,
	CodeDelimiter,
	Bullet,
	Para,
	Period
}

public class CiLexer
{
	enum PreDirectiveClass
	{
		IfOrElIf,
		Else
	}

	protected byte[] Input;
	internal bool HasErrors = false;
	int NextOffset;
	protected int CharOffset;
	int NextChar;
	protected string Filename;
	public int Line;
	int LexemeOffset;
	protected CiToken CurrentToken;
	protected long LongValue; // for CiToken.LiteralLong, CiToken.LiteralChar
	protected string StringValue; // for CiToken.LiteralString, CiToken.InterpolatedString, CiToken.Id
	public readonly HashSet<string> PreSymbols = new HashSet<string>();
	bool AtLineStart = true;
	bool LineMode = false;
	bool EnableDocComments = true;
	protected bool ParsingTypeArg = false;
	readonly Stack<PreDirectiveClass> PreStack = new Stack<PreDirectiveClass>();

	protected void Open(string filename, byte[] input)
	{
		this.Filename = filename;
		this.Input = input;
		this.NextOffset = 0;
		this.Line = 1;
		FillNextChar();
		if (this.NextChar == 0xfeff) // BOM
			FillNextChar();
		NextToken();
	}

	protected void ReportError(string message)
	{
		Console.Error.WriteLine($"{this.Filename}({this.Line}): ERROR: {message}");
		HasErrors = true;
	}

	CiException ParseException(string message) => new CiException(this.Filename, this.Line, message);

	int ReadByte() => this.NextOffset >= this.Input.Length ? -1 : this.Input[this.NextOffset++];

	int ReadContinuationByte(int hi)
	{
		int b = ReadByte();
		if (hi != 0xfffd) {
			if (b >= 0x80 && b <= 0xbf)
				return (hi << 6) + b - 0x80;
			ReportError("Invalid UTF-8");
		}
		return 0xfffd;
	}

	void FillNextChar()
	{
		this.CharOffset = NextOffset;
		int b = ReadByte();
		if (b >= 0x80) {
			if (b < 0xc2 || b > 0xf4) {
				ReportError("Invalid UTF-8");
				b = 0xfffd;
			}
			else if (b < 0xe0)
				b = ReadContinuationByte(b - 0xc0);
			else if (b < 0xf0) {
				b = ReadContinuationByte(b - 0xe0);
				b = ReadContinuationByte(b);
			}
			else {
				b = ReadContinuationByte(b - 0xf0);
				b = ReadContinuationByte(b);
				b = ReadContinuationByte(b);
			}
		}
		this.NextChar = b;
	}

	protected int PeekChar() => this.NextChar;

	public static bool IsLetterOrDigit(int c)
	{
		if (c >= 'a' && c <= 'z') return true;
		if (c >= 'A' && c <= 'Z') return true;
		if (c >= '0' && c <= '9') return true;
		return c == '_';
	}

	protected int ReadChar()
	{
		int c = this.NextChar;
		switch (c) {
		case '\t':
		case ' ':
			break;
		case '\n':
			this.Line++;
			this.AtLineStart = true;
			break;
		default:
			this.AtLineStart = false;
			break;
		}
		FillNextChar();
		return c;
	}

	bool EatChar(char c)
	{
		if (PeekChar() == c) {
			ReadChar();
			return true;
		}
		return false;
	}

	CiToken ReadIntegerLiteral(int bits)
	{
		bool invalidDigit = false;
		bool tooBig = false;
		bool needDigit = true;
		for (long i = 0;; ReadChar()) {
			int c = PeekChar();
			if (c >= '0' && c <= '9')
				c -= '0';
			else if (c >= 'A' && c <= 'Z')
				c -= 'A' - 10;
			else if (c >= 'a' && c <= 'z')
				c -= 'a' - 10;
			else if (c == '_') {
				needDigit = true;
				continue;
			}
			else {
				this.LongValue = i;
				if (invalidDigit || needDigit)
					ReportError("Invalid integer");
				else if (tooBig)
					ReportError("Integer too big");
				return CiToken.LiteralLong;
			}
			if (c >= 1 << bits)
				invalidDigit = true;
			else if (i >> (64 - bits) != 0)
				tooBig = true;
			else
				i = (i << bits) + c;
			needDigit = false;
		}
	}

	CiToken ReadFloatLiteral(bool needDigit)
	{
		bool underscoreE = false;
		for (;;) {
			int c = PeekChar();
			switch (c) {
			case '0':
			case '1':
			case '2':
			case '3':
			case '4':
			case '5':
			case '6':
			case '7':
			case '8':
			case '9':
			case '.':
				ReadChar();
				needDigit = false;
				break;
			case 'E':
			case 'e':
				if (needDigit)
					underscoreE = true;
				ReadChar();
				c = PeekChar();
				if (c == '+' || c == '-')
					ReadChar();
				needDigit = true;
				break;
			case '_':
				ReadChar();
				needDigit = true;
				break;
			default:
				if (underscoreE
				 || needDigit
				 || (c >= 'A' && c <= 'Z')
				 || (c >= 'a' && c <= 'z'))
					ReportError("Invalid floating-point number");
				return CiToken.LiteralDouble;
			}
		}
	}

	CiToken ReadNumberLiteral(long i)
	{
		bool leadingZero = false;
		bool tooBig = false;
		for (bool needDigit = false; ; ReadChar()) {
			int c = PeekChar();
			switch (c) {
			case '0':
			case '1':
			case '2':
			case '3':
			case '4':
			case '5':
			case '6':
			case '7':
			case '8':
			case '9':
				c -= '0';
				break;
			case '.':
			case 'e':
			case 'E':
				return ReadFloatLiteral(needDigit);
			case '_':
				needDigit = true;
				continue;
			default:
				this.LongValue = i;
				if (leadingZero)
					ReportError("Leading zeros are not permitted, octal numbers must begin with 0o");
				if (needDigit
				 || (c >= 'A' && c <= 'Z')
				 || (c >= 'a' && c <= 'z'))
					ReportError("Invalid integer");
				else if (tooBig)
					ReportError("Integer too big");
				return CiToken.LiteralLong;
			}
			if (i == 0)
				leadingZero = true;
			if (i > (c < 8 ? 922337203685477580 : 922337203685477579))
				tooBig = true;
			else
				i = 10 * i + c;
			needDigit = false;
		}
	}

	public static int GetEscapedChar(int c)
	{
		switch (c) {
		case '"': return '"';
		case '\'': return '\'';
		case '\\': return '\\';
		case 'a': return '\a';
		case 'b': return '\b';
		case 'f': return '\f';
		case 'n': return '\n';
		case 'r': return '\r';
		case 't': return '\t';
		case 'v': return '\v';
		default: return -1;
		}
	}

	int ReadCharLiteral()
	{
		int c = ReadChar();
		if (c < 32)
			throw ParseException("Invalid character in literal");
		if (c != '\\')
			return c;
		c = GetEscapedChar(ReadChar());
		if (c < 0)
			throw ParseException("Unknown escape sequence");
		return c;
	}

	protected CiToken ReadString(bool interpolated)
	{
		for (int offset = this.CharOffset; ; ) {
			int c = PeekChar();
			if (c == '"') {
				int endOffset = this.CharOffset;
				ReadChar();
				this.StringValue = Encoding.UTF8.GetString(this.Input, offset, endOffset - offset);
				return CiToken.LiteralString;
			}
			if (interpolated && c == '{') {
				int endOffset = this.CharOffset;
				ReadChar();
				if (PeekChar() != '{') {
					this.StringValue = Encoding.UTF8.GetString(this.Input, offset, endOffset - offset);
					return CiToken.InterpolatedString;
				}
			}
			ReadCharLiteral();
		}
	}

	protected string GetLexeme() => Encoding.UTF8.GetString(this.Input, this.LexemeOffset, this.CharOffset - this.LexemeOffset);

	string ReadId(int c)
	{
		if (!IsLetterOrDigit(c))
			throw ParseException("Invalid character");
		while (IsLetterOrDigit(PeekChar()))
			ReadChar();
		return GetLexeme();
	}

	CiToken ReadPreToken()
	{
		for (;;) {
			bool atLineStart = this.AtLineStart;
			this.LexemeOffset = this.CharOffset;
			int c = ReadChar();
			switch (c) {
			case -1:
				return CiToken.EndOfFile;
			case '\t':
			case '\r':
			case ' ':
				continue;
			case '\n':
				if (this.LineMode)
					return CiToken.EndOfLine;
				continue;
			case '#':
				if (!atLineStart)
					return CiToken.Hash;
				this.LexemeOffset = this.CharOffset;
				switch (ReadId(ReadChar())) {
				case "if": return CiToken.PreIf;
				case "elif": return CiToken.PreElIf;
				case "else": return CiToken.PreElse;
				case "endif": return CiToken.PreEndIf;
				default: throw ParseException("Unknown preprocessor directive");
				}
			case ';': return CiToken.Semicolon;
			case '.':
				if (EatChar('.')) return CiToken.Range;
				return CiToken.Dot;
			case ',': return CiToken.Comma;
			case '(': return CiToken.LeftParenthesis;
			case ')': return CiToken.RightParenthesis;
			case '[': return CiToken.LeftBracket;
			case ']': return CiToken.RightBracket;
			case '{': return CiToken.LeftBrace;
			case '}': return CiToken.RightBrace;
			case '~': return CiToken.Tilde;
			case '?': return CiToken.QuestionMark;
			case ':': return CiToken.Colon;
			case '+':
				if (EatChar('+')) return CiToken.Increment;
				if (EatChar('=')) return CiToken.AddAssign;
				return CiToken.Plus;
			case '-':
				if (EatChar('-')) return CiToken.Decrement;
				if (EatChar('=')) return CiToken.SubAssign;
				return CiToken.Minus;
			case '*':
				if (EatChar('=')) return CiToken.MulAssign;
				return CiToken.Asterisk;
			case '/':
				if (EatChar('/')) {
					c = ReadChar();
					if (c == '/' && this.EnableDocComments) {
						while (EatChar(' '));
						return CiToken.DocComment;
					}
					while (c != '\n' && c >= 0)
						c = ReadChar();
					if (c == '\n' && this.LineMode) return CiToken.EndOfLine;
					continue;
				}
				if (EatChar('*')) {
					int startLine = this.Line;
					do {
						c = ReadChar();
						if (c < 0)
							throw ParseException($"Unterminated multi-line comment, started in line {startLine}");
					} while (c != '*' || PeekChar() != '/');
					ReadChar();
					continue;
				}
				if (EatChar('=')) return CiToken.DivAssign;
				return CiToken.Slash;
			case '%':
				if (EatChar('=')) return CiToken.ModAssign;
				return CiToken.Mod;
			case '&':
				if (EatChar('&')) return CiToken.CondAnd;
				if (EatChar('=')) return CiToken.AndAssign;
				return CiToken.And;
			case '|':
				if (EatChar('|')) return CiToken.CondOr;
				if (EatChar('=')) return CiToken.OrAssign;
				return CiToken.Or;
			case '^':
				if (EatChar('=')) return CiToken.XorAssign;
				return CiToken.Xor;
			case '=':
				if (EatChar('=')) return CiToken.Equal;
				if (EatChar('>')) return CiToken.FatArrow;
				return CiToken.Assign;
			case '!':
				if (EatChar('=')) return CiToken.NotEqual;
				return CiToken.ExclamationMark;
			case '<':
				if (EatChar('<')) {
					if (EatChar('=')) return CiToken.ShiftLeftAssign;
					return CiToken.ShiftLeft;
				}
				if (EatChar('=')) return CiToken.LessOrEqual;
				return CiToken.Less;
			case '>':
				if (this.ParsingTypeArg) return CiToken.RightAngle;
				if (EatChar('>')) {
					if (EatChar('=')) return CiToken.ShiftRightAssign;
					return CiToken.ShiftRight;
				}
				if (EatChar('=')) return CiToken.GreaterOrEqual;
				return CiToken.Greater;
			case '\'':
				if (PeekChar() == '\'')
					throw ParseException("Empty character literal");
				this.LongValue = ReadCharLiteral();
				if (ReadChar() != '\'')
					throw ParseException("Unterminated character literal");
				return CiToken.LiteralChar;
			case '"':
				return ReadString(false);
			case '$':
				if (EatChar('"'))
					return ReadString(true);
				ReportError("Expected interpolated string");
				continue;
			case '0':
				switch (PeekChar()) {
				case 'B':
				case 'b':
					ReadChar();
					return ReadIntegerLiteral(1);
				case 'O':
				case 'o':
					ReadChar();
					return ReadIntegerLiteral(3);
				case 'X':
				case 'x':
					ReadChar();
					return ReadIntegerLiteral(4);
				default:
					return ReadNumberLiteral(0);
				}
			case '1':
			case '2':
			case '3':
			case '4':
			case '5':
			case '6':
			case '7':
			case '8':
			case '9':
				return ReadNumberLiteral(c - '0');
			default:
				string s = ReadId(c);
				switch (s) {
				case "abstract": return CiToken.Abstract;
				case "assert": return CiToken.Assert;
				case "break": return CiToken.Break;
				case "case": return CiToken.Case;
				case "class": return CiToken.Class;
				case "const": return CiToken.Const;
				case "continue": return CiToken.Continue;
				case "default": return CiToken.Default;
				case "do": return CiToken.Do;
				case "else": return CiToken.Else;
				case "enum": return CiToken.Enum;
				case "false": return CiToken.False;
				case "for": return CiToken.For;
				case "foreach": return CiToken.Foreach;
				case "if": return CiToken.If;
				case "in": return CiToken.In;
				case "internal": return CiToken.Internal;
				case "is": return CiToken.Is;
				case "lock": return CiToken.Lock;
				case "native": return CiToken.Native;
				case "new": return CiToken.New;
				case "null": return CiToken.Null;
				case "override": return CiToken.Override;
				case "protected": return CiToken.Protected;
				case "public": return CiToken.Public;
				case "resource": return CiToken.Resource;
				case "return": return CiToken.Return;
				case "sealed": return CiToken.Sealed;
				case "static": return CiToken.Static;
				case "switch": return CiToken.Switch;
				case "throw": return CiToken.Throw;
				case "throws": return CiToken.Throws;
				case "true": return CiToken.True;
				case "virtual": return CiToken.Virtual;
				case "void": return CiToken.Void;
				case "while": return CiToken.While;
				default:
					this.StringValue = s;
					return CiToken.Id;
				}
			}
		}
	}

	void NextPreToken()
	{
		this.CurrentToken = ReadPreToken();
	}

	bool EatPre(CiToken token)
	{
		if (See(token)) {
			NextPreToken();
			return true;
		}
		return false;
	}

	bool ParsePrePrimary()
	{
		if (EatPre(CiToken.ExclamationMark))
			return !ParsePrePrimary();
		if (EatPre(CiToken.LeftParenthesis)) {
			bool result = ParsePreOr();
			Check(CiToken.RightParenthesis);
			NextPreToken();
			return result;
		}
		if (See(CiToken.Id)) {
			bool result = this.PreSymbols.Contains(this.StringValue);
			NextPreToken();
			return result;
		}
		if (EatPre(CiToken.False))
			return false;
		if (EatPre(CiToken.True))
			return true;
		throw ParseException("Invalid preprocessor expression");
	}

	bool ParsePreEquality()
	{
		bool result = ParsePrePrimary();
		for (;;) {
			if (EatPre(CiToken.Equal))
				result = result == ParsePrePrimary();
			else if (EatPre(CiToken.NotEqual))
				result ^= ParsePrePrimary();
			else
				return result;
		}
	}

	bool ParsePreAnd()
	{
		bool result = ParsePreEquality();
		while (EatPre(CiToken.CondAnd))
			result &= ParsePreEquality();
		return result;
	}

	bool ParsePreOr()
	{
		bool result = ParsePreAnd();
		while (EatPre(CiToken.CondOr))
			result |= ParsePreAnd();
		return result;
	}

	bool ParsePreExpr()
	{
		this.LineMode = true;
		NextPreToken();
		bool result = ParsePreOr();
		Check(CiToken.EndOfLine);
		this.LineMode = false;
		return result;
	}

	void ExpectEndOfLine(string directive)
	{
		this.LineMode = true;
		CiToken token = ReadPreToken();
		if (token != CiToken.EndOfLine && token != CiToken.EndOfFile)
			throw ParseException($"Unexpected characters after {directive}");
		this.LineMode = false;
	}

	void PopPreStack(string directive)
	{
		if (this.PreStack.Count == 0)
			throw ParseException($"{directive} with no matching #if");
		PreDirectiveClass pdc = this.PreStack.Pop();
		if (directive != "#endif" && pdc == PreDirectiveClass.Else)
			throw ParseException($"{directive} after #else");
	}

	void SkipUntilPreMet()
	{
		this.EnableDocComments = false;
		for (;;) {
			// we are in a conditional that wasn't met yet
			switch (ReadPreToken()) {
			case CiToken.EndOfFile:
				throw ParseException("Expected #endif, got end of file");
			case CiToken.PreIf:
				ParsePreExpr();
				SkipUntilPreEndIf(false);
				break;
			case CiToken.PreElIf:
				if (ParsePreExpr()) {
					this.PreStack.Push(PreDirectiveClass.IfOrElIf);
					return;
				}
				break;
			case CiToken.PreElse:
				ExpectEndOfLine("#else");
				this.PreStack.Push(PreDirectiveClass.Else);
				return;
			case CiToken.PreEndIf:
				ExpectEndOfLine("#endif");
				return;
			}
		}
	}

	void SkipUntilPreEndIf(bool wasElse)
	{
		for (;;) {
			// we are in a conditional that was met before
			switch (ReadPreToken()) {
			case CiToken.EndOfFile:
				throw ParseException("Expected #endif, got end of file");
			case CiToken.PreIf:
				ParsePreExpr();
				SkipUntilPreEndIf(false);
				break;
			case CiToken.PreElIf:
				if (wasElse)
					throw ParseException("#elif after #else");
				ParsePreExpr();
				break;
			case CiToken.PreElse:
				if (wasElse)
					throw ParseException("#else after #else");
				ExpectEndOfLine("#else");
				wasElse = true;
				break;
			case CiToken.PreEndIf:
				ExpectEndOfLine("#endif");
				return;
			}
		}
	}

	CiToken ReadToken()
	{
		for (;;) {
			// we are in no conditionals or in all met
			this.EnableDocComments = true;
			CiToken token = ReadPreToken();
			switch (token) {
			case CiToken.EndOfFile:
				if (this.PreStack.Count != 0)
					throw ParseException("Expected #endif, got end of file");
				return CiToken.EndOfFile;
			case CiToken.PreIf:
				if (ParsePreExpr())
					this.PreStack.Push(PreDirectiveClass.IfOrElIf);
				else
					SkipUntilPreMet();
				break;
			case CiToken.PreElIf:
				PopPreStack("#elif");
				ParsePreExpr();
				this.EnableDocComments = false;
				SkipUntilPreEndIf(false);
				break;
			case CiToken.PreElse:
				PopPreStack("#else");
				ExpectEndOfLine("#else");
				this.EnableDocComments = false;
				SkipUntilPreEndIf(true);
				break;
			case CiToken.PreEndIf:
				PopPreStack("#endif");
				ExpectEndOfLine("#endif");
				break;
			default:
				return token;
			}
		}
	}

	protected CiToken NextToken()
	{
		CiToken token = this.CurrentToken;
		this.CurrentToken = ReadToken();
		return token;
	}

	protected bool See(CiToken token) => this.CurrentToken == token;

	protected bool Eat(CiToken token)
	{
		if (See(token)) {
			NextToken();
			return true;
		}
		return false;
	}

	protected bool Check(CiToken expected)
	{
		if (See(expected))
			return true;
		ReportError($"Expected {expected}, got {this.CurrentToken}");
		return false;
	}

	protected void Expect(CiToken expected)
	{
		Check(expected);
		NextToken();
	}

	protected void ExpectOrSkip(CiToken expected)
	{
		if (Check(expected))
			NextToken();
		else {
			do
				NextToken();
			while (!See(CiToken.EndOfFile) && !Eat(expected));
		}
	}

	protected bool DocCheckPeriod;
	protected int DocCurrentChar;
	CiDocToken DocCurrentToken;

	int DocReadChar()
	{
		int c = ReadChar();
		if (c == '\n') {
			NextToken();
			if (!See(CiToken.DocComment))
				return -1;
		}
		return c;
	}

	CiDocToken DocReadToken()
	{
		int lastChar = this.DocCurrentChar;
		for (;;) {
			int c = DocReadChar();
			this.DocCurrentChar = c;
			switch (c) {
			case -1:
				return CiDocToken.EndOfFile;
			case '`':
				return CiDocToken.CodeDelimiter;
			case '*':
				if (lastChar == '\n' && PeekChar() == ' ') {
					DocReadChar();
					return CiDocToken.Bullet;
				}
				return CiDocToken.Char;
			case '\r':
				continue;
			case '\n':
				if (this.DocCheckPeriod && lastChar == '.') {
					this.DocCheckPeriod = false;
					return CiDocToken.Period;
				}
				if (lastChar == '\n')
					return CiDocToken.Para;
				return CiDocToken.Char;
			default:
				return CiDocToken.Char;
			}
		}
	}

	protected void DocNextToken()
	{
		this.DocCurrentToken = DocReadToken();
	}

	protected bool DocSee(CiDocToken token) => this.DocCurrentToken == token;

	protected bool DocEat(CiDocToken token)
	{
		if (DocSee(token)) {
			DocNextToken();
			return true;
		}
		return false;
	}

	protected void DocExpect(CiDocToken expected)
	{
		if (!DocSee(expected))
			ReportError($"Expected {expected}, got {this.DocCurrentToken}");
		DocNextToken();
	}
}

}
