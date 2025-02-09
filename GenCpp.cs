// GenCpp.cs - C++ code generator
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
using System.IO;
using System.Linq;

namespace Foxoft.Ci
{

public class GenCpp : GenCCpp
{
	bool UsingStringViewLiterals;
	bool HasEnumFlags;

	protected override void IncludeStdInt() => Include("cstdint");

	protected override void IncludeAssert() => Include("cassert");

	protected override void IncludeMath() => Include("cmath");

	public override void VisitLiteralNull() => Write("nullptr");

	public override CiExpr VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent)
	{
		Include("format");
		Write("std::format(\"");
		foreach (CiInterpolatedPart part in expr.Parts) {
			WriteDoubling(part.Prefix, '{');
			Write("{}");
		}
		WriteDoubling(expr.Suffix, '{');
		WriteChar('"');
		WriteArgs(expr);
		WriteChar(')');
		return expr;
	}

	void WriteCamelCaseNotKeyword(string name)
	{
		WriteCamelCase(name);
		switch (name) {
		case "And":
		case "Asm":
		case "Auto":
		case "Bool":
		case "Break":
		case "Byte":
		case "Case":
		case "Catch":
		case "Char":
		case "Class":
		case "Const":
		case "Continue":
		case "Default":
		case "Delete":
		case "Do":
		case "Double":
		case "Else":
		case "Enum":
		case "Explicit":
		case "Export":
		case "Extern":
		case "False":
		case "Float":
		case "For":
		case "Goto":
		case "If":
		case "Inline":
		case "Int":
		case "Long":
		case "Namespace":
		case "New":
		case "Not":
		case "Nullptr":
		case "Operator":
		case "Or":
		case "Override":
		case "Private":
		case "Protected":
		case "Public":
		case "Register":
		case "Return":
		case "Short":
		case "Signed":
		case "Sizeof":
		case "Static":
		case "Struct":
		case "Switch":
		case "Throw":
		case "True":
		case "Try":
		case "Typedef":
		case "Union":
		case "Unsigned":
		case "Using":
		case "Virtual":
		case "Void":
		case "Volatile":
		case "While":
		case "and":
		case "asm":
		case "auto":
		case "catch":
		case "char":
		case "delete":
		case "explicit":
		case "export":
		case "extern":
		case "goto":
		case "inline":
		case "namespace":
		case "not":
		case "nullptr":
		case "operator":
		case "or":
		case "private":
		case "register":
		case "signed":
		case "sizeof":
		case "struct":
		case "try":
		case "typedef":
		case "union":
		case "unsigned":
		case "using":
		case "volatile":
			WriteChar('_');
			break;
		default:
			break;
		}
	}

	protected override void WriteName(CiSymbol symbol)
	{
		switch (symbol) {
		case CiContainerType _:
			Write(symbol.Name);
			break;
		case CiVar _:
		case CiMember _:
			WriteCamelCaseNotKeyword(symbol.Name);
			break;
		default:
			throw new NotImplementedException(symbol.GetType().Name);
		}
	}

	protected override void WriteLocalName(CiSymbol symbol, CiPriority parent)
	{
		if (symbol is CiField)
			Write("this->");
		WriteName(symbol);
	}

	void WriteCollectionType(string name, CiType elementType)
	{
		Include(name);
		Write("std::");
		Write(name);
		WriteChar('<');
		WriteType(elementType, false);
		WriteChar('>');
	}

	protected override void WriteType(CiType type, bool promote)
	{
		switch (type) {
		case CiIntegerType integer:
			WriteTypeCode(GetIntegerTypeCode(integer, promote));
			break;
		case CiDynamicPtrType dynamic:
			switch (dynamic.Class.Id) {
			case CiId.RegexClass:
				Include("regex");
				Write("std::regex");
				break;
			case CiId.ArrayPtrClass:
				Include("memory");
				Write("std::shared_ptr<");
				WriteType(dynamic.GetElementType(), false);
				Write("[]>");
				break;
			default:
				Include("memory");
				Write("std::shared_ptr<");
				Write(dynamic.Class.Name);
				WriteChar('>');
				break;
			}
			break;
		case CiClassType klass:
			if (klass.Class.TypeParameterCount == 0) {
				if (klass.Class.Id == CiId.StringClass) {
					string cppType = klass.IsNullable() ? "string_view" : "string";
					Include(cppType);
					Write("std::");
					Write(cppType);
					break;
				}
				if (!(klass is CiReadWriteClassType))
					Write("const ");
				switch (klass.Class.Id) {
				case CiId.RegexClass:
					Include("regex");
					Write("std::regex");
					break;
				case CiId.MatchClass:
					Include("regex");
					Write("std::cmatch");
					break;
				case CiId.LockClass:
					Include("mutex");
					Write("std::recursive_mutex");
					break;
				default:
					Write(klass.Class.Name);
					break;
				}
			}
			else if (klass.Class.Id == CiId.ArrayPtrClass) {
				WriteType(klass.GetElementType(), false);
				if (!(klass is CiReadWriteClassType))
					Write(" const");
			}
			else {
				string cppType = klass.Class.Id switch {
					CiId.ArrayStorageClass => "array",
					CiId.ListClass => "vector",
					CiId.QueueClass => "queue",
					CiId.StackClass => "stack",
					CiId.HashSetClass => "unordered_set",
					CiId.DictionaryClass => "unordered_map",
					CiId.SortedDictionaryClass => "map",
					_ => throw new NotImplementedException()
				};
				Include(cppType);
				if (!(klass is CiReadWriteClassType))
					Write("const ");
				Write("std::");
				Write(cppType);
				WriteChar('<');
				WriteType(klass.TypeArg0, false);
				if (klass is CiArrayStorageType arrayStorage) {
					Write(", ");
					VisitLiteralLong(arrayStorage.Length);
				}
				else if (klass.Class.TypeParameterCount == 2) {
					Write(", ");
					WriteType(klass.GetValueType(), false);
				}
				WriteChar('>');
			}
			if (!(klass is CiStorageType))
				Write(" *");
			break;
		default:
			Write(type.Name);
			break;
		}
	}

	protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent)
	{
		Include("memory");
		Write("std::make_shared<");
		WriteType(elementType, false);
		Write("[]>(");
		lengthExpr.Accept(this, CiPriority.Argument);
		WriteChar(')');
	}

	protected override void WriteNew(CiReadWriteClassType klass, CiPriority parent)
	{
		Include("memory");
		Write("std::make_shared<");
		Write(klass.Class.Name);
		Write(">()");
	}

	protected override void WriteVarInit(CiNamedValue def)
	{
		if (def.Value != null && def.Type == CiSystem.StringStorageType) {
			WriteChar('{');
			def.Value.Accept(this, CiPriority.Argument);
			WriteChar('}');
		}
		else if (def.Type is CiArrayStorageType) {
			switch (def.Value) {
			case null:
				break;
			case CiLiteral literal when literal.IsDefaultValue():
				Write(" {}");
				break;
			default:
				throw new NotImplementedException("Only null, zero and false supported");
			}
		}
		else if (def.Value == null && def.Type is CiStorageType) {
		}
		else
			base.WriteVarInit(def);
	}

	protected override void WriteStaticCast(CiType type, CiExpr expr)
	{
		if (type is CiDynamicPtrType dynamic) {
			Write("std::static_pointer_cast<");
			Write(dynamic.Class.Name);
		}
		else {
			Write("static_cast<");
			WriteType(type, false);
		}
		Write(">(");
		GetStaticCastInner(type, expr).Accept(this, CiPriority.Argument);
		WriteChar(')');
	}

	protected override bool HasInitCode(CiNamedValue def) => false;

	protected override void WriteInitCode(CiNamedValue def)
	{
	}

	static bool NeedStringPtrData(CiExpr expr)
	{
		if (expr.Type != CiSystem.StringPtrType)
			return false;
		if (expr is CiCallExpr call && call.Method.Symbol.Id == CiId.EnvironmentGetEnvironmentVariable)
			return false;
		return true;
	}

	protected override void WriteEqual(CiBinaryExpr expr, CiPriority parent, bool not)
	{
		if (NeedStringPtrData(expr.Left) && expr.Right.Type == CiSystem.NullType) {
			WriteCoerced(CiSystem.StringPtrType, expr.Left, CiPriority.Primary);
			Write(".data()");
			Write(GetEqOp(not));
			Write("nullptr");
		}
		else if (expr.Left.Type == CiSystem.NullType && NeedStringPtrData(expr.Right)) {
			Write("nullptr");
			Write(GetEqOp(not));
			WriteCoerced(CiSystem.StringPtrType, expr.Right, CiPriority.Primary);
			Write(".data() ");
		}
		else
			base.WriteEqual(expr, parent, not);
	}

	static bool IsClassPtr(CiType type) => type is CiClassType ptr && !(type is CiStorageType) && ptr.Class.Id != CiId.StringClass && ptr.Class.Id != CiId.ArrayPtrClass;

	static bool IsCppPtr(CiExpr expr)
	{
		if (IsClassPtr(expr.Type)) {
			if (expr is CiSymbolReference symbol
			 && symbol.Symbol.Parent is CiForeach loop
			 && loop.Collection.Type is CiArrayStorageType array
			 && array.GetElementType() is CiStorageType)
				return false; // C++ reference
			return true; // C++ pointer
		}
		return false;
	}

	protected override void WriteIndexing(CiBinaryExpr expr, CiPriority parent)
	{
		if (IsClassPtr(expr.Left.Type)) {
			Write("(*");
			expr.Left.Accept(this, CiPriority.Primary);
			Write(")[");
			expr.Right.Accept(this, CiPriority.Argument);
			WriteChar(']');
		}
		else
			base.WriteIndexing(expr, parent);
	}

	void WriteMemberOp(CiExpr left)
	{
		if (IsCppPtr(left))
			Write("->");
		else
			WriteChar('.');
	}

	protected override void WriteMemberOp(CiExpr left, CiSymbolReference symbol)
	{
		if (symbol != null && symbol.Symbol is CiConst) // FIXME
			Write("::");
		else
			WriteMemberOp(left);
	}

	void StartMethodCall(CiExpr obj)
	{
		obj.Accept(this, CiPriority.Primary);
		WriteMemberOp(obj);
	}

	void WriteCollectionObject(CiExpr obj, CiPriority priority)
	{
		if (obj.Type is CiStorageType)
			obj.Accept(this, priority);
		else {
			WriteChar('*');
			obj.Accept(this, CiPriority.Primary);
		}
	}

	void WriteBeginEnd(CiExpr obj)
	{
		StartMethodCall(obj);
		Write("begin(), ");
		StartMethodCall(obj); // FIXME: side effect
		Write("end()");
	}

	void WriteNotRawStringLiteral(CiExpr obj, CiPriority priority)
	{
		obj.Accept(this, priority);
		if (obj is CiLiteralString) {
			Include("string_view");
			this.UsingStringViewLiterals = true;
			Write("sv");
		}
	}

	void WriteStringMethod(CiExpr obj, string name, CiMethod method, List<CiExpr> args)
	{
		WriteNotRawStringLiteral(obj, CiPriority.Primary);
		WriteChar('.');
		Write(name);
		if (IsOneAsciiString(args[0], out char c)) {
			WriteChar('(');
			VisitLiteralChar(c);
			WriteChar(')');
		}
		else
			WriteArgsInParentheses(method, args);
	}

	void WriteRegex(List<CiExpr> args, int argIndex)
	{
		Include("regex");
		Write("std::regex(");
		args[argIndex].Accept(this, CiPriority.Argument);
		WriteRegexOptions(args, ", std::regex::ECMAScript | ", " | ", "", "std::regex::icase", "std::regex::multiline", "std::regex::NOT_SUPPORTED_singleline");
		WriteChar(')');
	}

	void WriteConsoleWrite(CiExpr obj, List<CiExpr> args, bool newLine)
	{
		Include("iostream");
		Write(obj.IsReferenceTo(CiSystem.ConsoleError) ? "std::cerr" : "std::cout");
		if (args.Count == 1) {
			if (args[0] is CiInterpolatedString interpolated) {
				bool uppercase = false;
				bool hex = false;
				char flt = 'G';
				foreach (CiInterpolatedPart part in interpolated.Parts) {
					switch (part.Format) {
					case 'E':
					case 'G':
					case 'X':
						if (!uppercase) {
							Write(" << std::uppercase");
							uppercase = true;
						}
						break;
					case 'e':
					case 'g':
					case 'x':
						if (uppercase) {
							Write(" << std::nouppercase");
							uppercase = false;
						}
						break;
					default:
						break;
					}

					switch (part.Format) {
					case 'E':
					case 'e':
						if (flt != 'E') {
							Write(" << std::scientific");
							flt = 'E';
						}
						break;
					case 'F':
					case 'f':
						if (flt != 'F') {
							Write(" << std::fixed");
							flt = 'F';
						}
						break;
					case 'X':
					case 'x':
						if (!hex) {
							Write(" << std::hex");
							hex = true;
						}
						break;
					default:
						if (hex) {
							Write(" << std::dec");
							hex = false;
						}
						if (flt != 'G') {
							Write(" << std::defaultfloat");
							flt = 'G';
						}
						break;
					}

					if (part.Prefix.Length > 0) {
						Write(" << ");
						VisitLiteralString(part.Prefix);
					}

					Write(" << ");
					part.Argument.Accept(this, CiPriority.Mul);
				}

				if (uppercase)
					Write(" << std::nouppercase");
				if (hex)
					Write(" << std::dec");
				if (flt != 'G')
					Write(" << std::defaultfloat");
				if (interpolated.Suffix.Length > 0) {
					Write(" << ");
					if (newLine) {
						WriteStringLiteralWithNewLine(interpolated.Suffix);
						return;
					}
					VisitLiteralString(interpolated.Suffix);
				}
			}
			else {
				Write(" << ");
				if (newLine && args[0] is CiLiteralString literal) {
					WriteStringLiteralWithNewLine(literal.Value);
					return;
				}
				args[0].Accept(this, CiPriority.Mul);
			}
		}
		if (newLine)
			Write(" << '\\n'");
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, List<CiExpr> args, CiPriority parent)
	{
		switch (method.Id) {
		case CiId.StringContains:
			if (parent > CiPriority.Equality)
				WriteChar('(');
			WriteStringMethod(obj, "find", method, args);
			Write(" != std::string::npos");
			if (parent > CiPriority.Equality)
				WriteChar(')');
			break;
		case CiId.StringEndsWith:
			WriteStringMethod(obj, "ends_with", method, args);
			break;
		case CiId.StringIndexOf:
			Write("static_cast<int>(");
			WriteStringMethod(obj, "find", method, args);
			WriteChar(')');
			break;
		case CiId.StringLastIndexOf:
			Write("static_cast<int>(");
			WriteStringMethod(obj, "rfind", method, args);
			WriteChar(')');
			break;
		case CiId.StringStartsWith:
			WriteStringMethod(obj, "starts_with", method, args);
			break;
		case CiId.StringSubstring:
			WriteStringMethod(obj, "substr", method, args);
			break;
		case CiId.ArrayBinarySearchAll:
		case CiId.ArrayBinarySearchPart:
			Include("algorithm");
			if (parent > CiPriority.Add)
				WriteChar('(');
			Write("std::lower_bound(");
			if (args.Count == 1)
				WriteBeginEnd(obj);
			else {
				WriteArrayPtrAdd(obj, args[1]);
				Write(", ");
				WriteArrayPtrAdd(obj, args[1]); // FIXME: side effect
				Write(" + ");
				args[2].Accept(this, CiPriority.Add);
			}
			Write(", ");
			args[0].Accept(this, CiPriority.Argument);
			Write(") - ");
			WriteArrayPtr(obj, CiPriority.Mul);
			if (parent > CiPriority.Add)
				WriteChar(')');
			break;
		case CiId.ArrayCopyTo:
		case CiId.ListCopyTo:
			Include("algorithm");
			Write("std::copy_n(");
			WriteArrayPtrAdd(obj, args[0]);
			Write(", ");
			args[3].Accept(this, CiPriority.Argument);
			Write(", ");
			WriteArrayPtrAdd(args[1], args[2]);
			WriteChar(')');
			break;
		case CiId.ArrayFillAll:
			StartMethodCall(obj);
			Write("fill(");
			WriteCoerced(((CiClassType) obj.Type).GetElementType(), args[0], CiPriority.Argument);
			WriteChar(')');
			break;
		case CiId.ArrayFillPart:
			Include("algorithm");
			Write("std::fill_n(");
			WriteArrayPtrAdd(obj, args[1]);
			Write(", ");
			args[2].Accept(this, CiPriority.Argument);
			Write(", ");
			args[0].Accept(this, CiPriority.Argument);
			WriteChar(')');
			break;
		case CiId.ArraySortAll:
		case CiId.ListSortAll:
			Include("algorithm");
			Write("std::sort(");
			WriteBeginEnd(obj);
			WriteChar(')');
			break;
		case CiId.ArraySortPart:
		case CiId.ListSortPart:
			Include("algorithm");
			Write("std::sort(");
			WriteArrayPtrAdd(obj, args[0]);
			Write(", ");
			WriteArrayPtrAdd(obj, args[0]); // FIXME: side effect
			Write(" + ");
			args[1].Accept(this, CiPriority.Add);
			WriteChar(')');
			break;
		case CiId.ListAdd:
			StartMethodCall(obj);
			if (args.Count == 0)
				Write("emplace_back()");
			else {
				Write("push_back(");
				WriteCoerced(((CiClassType) obj.Type).GetElementType(), args[0], CiPriority.Argument);
				WriteChar(')');
			}
			break;
		case CiId.ListAny:
			Include("algorithm");
			Write("std::any_of(");
			WriteBeginEnd(obj);
			Write(", ");
			args[0].Accept(this, CiPriority.Argument);
			WriteChar(')');
			break;
		case CiId.ListContains:
			Include("algorithm");
			if (parent > CiPriority.Equality)
				WriteChar('(');
			Write("std::find(");
			WriteBeginEnd(obj);
			Write(", ");
			args[0].Accept(this, CiPriority.Argument);
			Write(") != ");
			StartMethodCall(obj); // FIXME: side effect
			Write("end()");
			if (parent > CiPriority.Equality)
				WriteChar(')');
			break;
		case CiId.ListInsert:
			StartMethodCall(obj);
			if (args.Count == 1) {
				Write("emplace(");
				WriteArrayPtrAdd(obj, args[0]); // FIXME: side effect
			}
			else {
				Write("insert(");
				WriteArrayPtrAdd(obj, args[0]); // FIXME: side effect
				Write(", ");
				WriteCoerced(((CiClassType) obj.Type).GetElementType(), args[1], CiPriority.Argument);
			}
			WriteChar(')');
			break;
		case CiId.ListRemoveAt:
			StartMethodCall(obj);
			Write("erase(");
			WriteArrayPtrAdd(obj, args[0]); // FIXME: side effect
			WriteChar(')');
			break;
		case CiId.ListRemoveRange:
			StartMethodCall(obj);
			Write("erase(");
			WriteArrayPtrAdd(obj, args[0]); // FIXME: side effect
			Write(", ");
			WriteArrayPtrAdd(obj, args[0]); // FIXME: side effect
			Write(" + ");
			args[1].Accept(this, CiPriority.Add);
			WriteChar(')');
			break;
		case CiId.QueueClear:
		case CiId.StackClear:
			WriteCollectionObject(obj, CiPriority.Assign);
			Write(" = {}");
			break;
		case CiId.QueueDequeue:
			if (parent == CiPriority.Statement) {
				StartMethodCall(obj);
				Write("pop()");
			}
			else {
				// :-)
				CiType elementType = ((CiClassType) obj.Type).GetElementType();
				Write("[](");
				WriteCollectionType("queue", elementType);
				Write(" &q) { ");
				WriteType(elementType, false);
				Write(" front = q.front(); q.pop(); return front; }(");
				WriteCollectionObject(obj, CiPriority.Argument);
				WriteChar(')');
			}
			break;
		case CiId.QueueEnqueue:
			WriteCall(obj, "push", args[0]);
			break;
		case CiId.QueuePeek:
			StartMethodCall(obj);
			Write("front()");
			break;
		case CiId.StackPeek:
			StartMethodCall(obj);
			Write("top()");
			break;
		case CiId.StackPop:
			if (parent == CiPriority.Statement) {
				StartMethodCall(obj);
				Write("pop()");
			}
			else {
				// :-)
				CiType elementType = ((CiClassType) obj.Type).GetElementType();
				Write("[](");
				WriteCollectionType("stack", elementType);
				Write(" &s) { ");
				WriteType(elementType, false);
				Write(" top = s.top(); s.pop(); return top; }(");
				WriteCollectionObject(obj, CiPriority.Argument);
				WriteChar(')');
			}
			break;
		case CiId.HashSetAdd:
			WriteCall(obj, ((CiClassType) obj.Type).GetElementType() == CiSystem.StringStorageType && args[0].Type == CiSystem.StringPtrType ? "emplace" : "insert", args[0]);
			break;
		case CiId.HashSetRemove:
		case CiId.DictionaryRemove:
		case CiId.SortedDictionaryRemove:
			WriteCall(obj, "erase", args[0]);
			break;
		case CiId.DictionaryAdd:
			WriteIndexing(obj, args[0]);
			break;
		case CiId.DictionaryContainsKey:
		case CiId.SortedDictionaryContainsKey:
			if (parent > CiPriority.Equality)
				WriteChar('(');
			StartMethodCall(obj);
			Write("count");
			WriteArgsInParentheses(method, args);
			Write(" != 0");
			if (parent > CiPriority.Equality)
				WriteChar(')');
			break;
		case CiId.ConsoleWrite:
			WriteConsoleWrite(obj, args, false);
			break;
		case CiId.ConsoleWriteLine:
			WriteConsoleWrite(obj, args, true);
			break;
		case CiId.UTF8GetByteCount:
			if (args[0] is CiLiteral) {
				if (parent > CiPriority.Add)
					WriteChar('(');
				Write("sizeof(");
				args[0].Accept(this, CiPriority.Argument);
				Write(") - 1");
				if (parent > CiPriority.Add)
					WriteChar(')');
			}
			else
				WriteStringLength(args[0]);
			break;
		case CiId.UTF8GetBytes:
			if (args[0] is CiLiteral) {
				Include("algorithm");
				Write("std::copy_n(");
				args[0].Accept(this, CiPriority.Argument);
				Write(", sizeof(");
				args[0].Accept(this, CiPriority.Argument);
				Write(") - 1, ");
				WriteArrayPtrAdd(args[1], args[2]);
				WriteChar(')');
			}
			else {
				args[0].Accept(this, CiPriority.Primary);
				Write(".copy(reinterpret_cast<char *>("); // cast pointer signedness
				WriteArrayPtrAdd(args[1], args[2]);
				Write("), ");
				args[0].Accept(this, CiPriority.Primary); // FIXME: side effect
				Write(".size())");
			}
			break;
		case CiId.UTF8GetString:
			Include("string_view");
			Write("std::string_view(reinterpret_cast<const char *>(");
			WriteArrayPtrAdd(args[0], args[1]);
			Write("), ");
			args[2].Accept(this, CiPriority.Argument);
			WriteChar(')');
			break;
		case CiId.EnvironmentGetEnvironmentVariable:
			Include("cstdlib");
			Write("std::getenv(");
			if (args[0] is CiLiteralString)
				args[0].Accept(this, CiPriority.Argument);
			else {
				args[0].Accept(this, CiPriority.Primary);
				Write(".data()");
			}
			WriteChar(')');
			break;
		case CiId.RegexCompile:
			WriteRegex(args, 0);
			break;
		case CiId.RegexIsMatchStr:
		case CiId.RegexIsMatchRegex:
		case CiId.MatchFindStr:
		case CiId.MatchFindRegex:
			Write("std::regex_search(");
			if (args[0].Type == CiSystem.StringPtrType && !(args[0] is CiLiteral))
				WriteBeginEnd(args[0]);
			else
				args[0].Accept(this, CiPriority.Argument);
			if (method.Id == CiId.MatchFindStr || method.Id == CiId.MatchFindRegex) {
				Write(", ");
				obj.Accept(this, CiPriority.Argument);
			}
			Write(", ");
			if (method.Id == CiId.RegexIsMatchRegex)
				obj.Accept(this, CiPriority.Argument);
			else if (method.Id == CiId.MatchFindRegex)
				args[1].Accept(this, CiPriority.Argument);
			else
				WriteRegex(args, 1);
			WriteChar(')');
			break;
		case CiId.MatchGetCapture:
			StartMethodCall(obj);
			WriteCall("str", args[0]);
			break;
		case CiId.MathMethod:
		case CiId.MathIsFinite:
		case CiId.MathIsNaN:
		case CiId.MathLog2:
			Include("cmath");
			Write("std::");
			WriteLowercase(method.Name);
			WriteArgsInParentheses(method, args);
			break;
		case CiId.MathCeiling:
			Include("cmath");
			WriteCall("std::ceil", args[0]);
			break;
		case CiId.MathFusedMultiplyAdd:
			Include("cmath");
			WriteCall("std::fma", args[0], args[1], args[2]);
			break;
		case CiId.MathIsInfinity:
			Include("cmath");
			WriteCall("std::isinf", args[0]);
			break;
		case CiId.MathTruncate:
			Include("cmath");
			WriteCall("std::trunc", args[0]);
			break;
		default:
			if (obj != null) {
				if (obj.IsReferenceTo(CiSystem.BasePtr)) {
					WriteName((CiClass) method.Parent);
					Write("::");
				}
				else {
					obj.Accept(this, CiPriority.Primary);
					if (method.CallType == CiCallType.Static)
						Write("::");
					else
						WriteMemberOp(obj);
				}
			}
			WriteName(method);
			WriteArgsInParentheses(method, args);
			break;
		}
	}

	protected override void WriteResource(string name, int length)
	{
		if (length >= 0) // reference as opposed to definition
			Write("CiResource::");
		foreach (char c in name)
			WriteChar(CiLexer.IsLetterOrDigit(c) ? c : '_');
	}

	protected override void WriteArrayPtr(CiExpr expr, CiPriority parent)
	{
		switch (expr.Type) {
		case CiArrayStorageType _:
		case CiStringType _:
			expr.Accept(this, CiPriority.Primary);
			Write(".data()");
			break;
		case CiDynamicPtrType _:
			expr.Accept(this, CiPriority.Primary);
			Write(".get()");
			break;
		case CiClassType klass when klass.Class.Id == CiId.ListClass:
			StartMethodCall(expr);
			Write("begin()");
			break;
		default:
			expr.Accept(this, parent);
			break;
		}
	}

	protected override void WriteCoercedInternal(CiType type, CiExpr expr, CiPriority parent)
	{
		if (type is CiClassType klass && !(klass is CiDynamicPtrType) && !(klass is CiStorageType)) {
			if (klass.Class.Id == CiId.StringClass) {
				if (expr.Type == CiSystem.NullType) {
					Include("string_view");
					Write("std::string_view(nullptr, 0)");
				}
				else
					expr.Accept(this, parent);
				return;
			}
			if (klass.Class.Id == CiId.ArrayPtrClass) {
				WriteArrayPtr(expr, parent);
				return;
			}
			switch (expr.Type) {
			case CiDynamicPtrType _:
				expr.Accept(this, CiPriority.Primary);
				Write(".get()");
				return;
			case CiClassType _ when !IsCppPtr(expr):
				WriteChar('&');
				if (expr is CiCallExpr) {
					Write("static_cast<");
					if (!(klass is CiReadWriteClassType))
						Write("const ");
					WriteName(klass.Class);
					Write(" &>(");
					expr.Accept(this, CiPriority.Argument);
					WriteChar(')');
				}
				else
					expr.Accept(this, CiPriority.Primary);
				return;
			default:
				break;
			}
		}
		base.WriteCoercedInternal(type, expr, parent);
	}

	protected override void WriteEqualString(CiExpr left, CiExpr right, CiPriority parent, bool not)
	{
		left.Accept(this, CiPriority.Equality);
		Write(GetEqOp(not));
		right.Accept(this, CiPriority.Equality);
	}

	protected override void WriteStringLength(CiExpr expr)
	{
		WriteNotRawStringLiteral(expr, CiPriority.Primary);
		Write(".length()");
	}

	void WriteMatchProperty(CiSymbolReference expr, string name)
	{
		StartMethodCall(expr.Left);
		Write(name);
		Write("()");
	}

	public override CiExpr VisitSymbolReference(CiSymbolReference expr, CiPriority parent)
	{
		switch (expr.Symbol.Id) {
		case CiId.ListCount:
		case CiId.QueueCount:
		case CiId.StackCount:
		case CiId.HashSetCount:
		case CiId.DictionaryCount:
		case CiId.SortedDictionaryCount:
		case CiId.OrderedDictionaryCount:
			expr.Left.Accept(this, CiPriority.Primary);
			WriteMemberOp(expr.Left, expr);
			Write("size()");
			return expr;
		case CiId.MatchStart:
			WriteMatchProperty(expr, "position");
			return expr;
		case CiId.MatchEnd:
			if (parent > CiPriority.Add)
				WriteChar('(');
			WriteMatchProperty(expr, "position");
			Write(" + ");
			WriteMatchProperty(expr, "length"); // FIXME: side effect
			if (parent > CiPriority.Add)
				WriteChar(')');
			return expr;
		case CiId.MatchLength:
			WriteMatchProperty(expr, "length");
			return expr;
		case CiId.MatchValue:
			WriteMatchProperty(expr, "str");
			return expr;
		default:
			return base.VisitSymbolReference(expr, parent);
		}
	}

	public override CiExpr VisitBinaryExpr(CiBinaryExpr expr, CiPriority parent)
	{
		switch (expr.Op) {
		case CiToken.Equal:
		case CiToken.NotEqual:
		case CiToken.Greater:
			if (IsStringEmpty(expr, out CiExpr str)) {
				if (expr.Op != CiToken.Equal)
					WriteChar('!');
				str.Accept(this, CiPriority.Primary);
				Write(".empty()");
				return expr;
			}
			break;
		case CiToken.Assign when expr.Left.Type == CiSystem.StringStorageType && parent == CiPriority.Statement && IsTrimSubstring(expr) is CiExpr length:
			WriteCall(expr.Left, "resize", length);
			return expr;
		case CiToken.Is:
			Write("dynamic_cast<const ");
			Write(((CiClass) expr.Right).Name);
			Write(" *>(");
			expr.Left.Accept(this, CiPriority.Argument);
			WriteChar(')');
			return expr;
		default:
			break;
		}
		return base.VisitBinaryExpr(expr, parent);
	}

	public override void VisitLambdaExpr(CiLambdaExpr expr)
	{
		Write("[](const ");
		WriteType(expr.First.Type, false);
		Write(" &");
		WriteName(expr.First);
		Write(") { return ");
		expr.Body.Accept(this, CiPriority.Argument);
		Write("; }");
	}

	protected override void WriteConst(CiConst konst)
	{
		Write("static constexpr ");
		WriteTypeAndName(konst);
		Write(" = ");
		konst.Value.Accept(this, CiPriority.Argument);
		WriteLine(';');
	}

	public override void VisitForeach(CiForeach statement)
	{
		CiVar element = statement.GetVar();
		Write("for (");
		if (statement.Count() == 2) {
			Write("const auto &[");
			Write(element.Name);
			Write(", ");
			Write(statement.GetValueVar().Name);
			WriteChar(']');
		}
		else if (statement.Collection.Type is CiArrayStorageType array
		 && array.GetElementType() is CiStorageType storage
		 && element.Type is CiClassType ptr) {
			if (!(ptr is CiReadWriteClassType))
				Write("const ");
			Write(storage.Class.Name);
			Write(" &");
			Write(element.Name);
		}
		else
			WriteTypeAndName(element);
		Write(" : ");
		WriteNotRawStringLiteral(statement.Collection, CiPriority.Argument);
		WriteChar(')');
		WriteChild(statement.Body);
	}

	public override void VisitLock(CiLock statement)
	{
		OpenBlock();
		Write("const std::lock_guard<std::recursive_mutex> lock(");
		statement.Lock.Accept(this, CiPriority.Argument);
		WriteLine(");");
		FlattenBlock(statement.Body);
		CloseBlock();
	}

	protected override void WriteReturnValue(CiExpr value)
	{
		if (this.CurrentMethod.Type == CiSystem.StringStorageType
		 && value.Type == CiSystem.StringPtrType
		 && !(value is CiLiteral)) {
			Write("std::string(");
			base.WriteReturnValue(value);
			WriteChar(')');
		}
		else if (this.CurrentMethod.Type == CiSystem.StringStorageType
			&& IsStringSubstring(value, out bool cast, out CiExpr ptr, out CiExpr offset, out CiExpr length)
			&& ptr.Type != CiSystem.StringStorageType) {
			Write("std::string(");
			if (cast)
				Write("reinterpret_cast<const char *>(");
			WriteArrayPtrAdd(ptr, offset);
			if (cast)
				WriteChar(')');
			Write(", ");
			length.Accept(this, CiPriority.Argument);
			WriteChar(')');
		}
		else
			base.WriteReturnValue(value);
	}

	protected override void WriteCaseBody(List<CiStatement> statements)
	{
		bool block = false;
		foreach (CiStatement statement in statements) {
			if (!block && statement is CiVar) {
				OpenBlock();
				block = true;
			}
			statement.AcceptStatement(this);
		}
		if (block)
			CloseBlock();
	}

	public override void VisitSwitch(CiSwitch statement)
	{
		if (statement.Value.Type is CiClassType klass && klass.Class.Id != CiId.StringClass) {
			int gotoId = GetSwitchGoto(statement);
			string op = "if (";
			foreach (CiCase kase in statement.Cases) {
				if (kase.Values.Count != 1)
					throw new NotImplementedException();
				Write(op);
				CiVar def = (CiVar) kase.Values[0];
				WriteTypeAndName(def);
				Write(" = dynamic_cast<");
				WriteType(def.Type, false);
				Write(">(");
				statement.Value.Accept(this, CiPriority.Argument); // FIXME: side effect in every if
				Write("))");
				WriteIfCaseBody(kase.Body, gotoId < 0);
				op = "else if (";
			}
			EndSwitchAsIfs(statement, gotoId);
		}
		else
			base.VisitSwitch(statement);
	}

	public override void VisitThrow(CiThrow statement)
	{
		Include("exception");
		WriteLine("throw std::exception();");
		// TODO: statement.Message.Accept(this, CiPriority.Argument);
	}

	void OpenNamespace()
	{
		if (this.Namespace == null)
			return;
		WriteLine();
		Write("namespace ");
		WriteLine(this.Namespace);
		WriteLine('{');
	}

	void CloseNamespace()
	{
		if (this.Namespace != null)
			WriteLine('}');
	}

	protected override void WriteEnum(CiEnum enu)
	{
		WriteLine();
		WriteDoc(enu.Documentation);
		Write("enum class ");
		WriteLine(enu.Name);
		OpenBlock();
		enu.AcceptValues(this);
		WriteLine();
		this.Indent--;
		WriteLine("};");
		if (enu is CiEnumFlags) {
			Include("type_traits");
			this.HasEnumFlags = true;
			Write("CI_ENUM_FLAG_OPERATORS(");
			Write(enu.Name);
			WriteLine(')');
		}
	}

	static CiVisibility GetConstructorVisibility(CiClass klass)
	{
		switch (klass.CallType) {
		case CiCallType.Static:
			return CiVisibility.Private;
		case CiCallType.Abstract:
			return CiVisibility.Protected;
		default:
			return CiVisibility.Public;
		}
	}

	static bool HasMembersOfVisibility(CiClass klass, CiVisibility visibility)
	{
		for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
			if (symbol is CiMember member && member.Visibility == visibility)
				return true;
		}
		return false;
	}

	protected override void WriteField(CiField field)
	{
		WriteDoc(field.Documentation);
		WriteVar(field);
		WriteLine(';');
	}

	void WriteParametersAndConst(CiMethod method, bool defaultArguments)
	{
		WriteParameters(method, defaultArguments);
		if (method.CallType != CiCallType.Static && !method.IsMutator)
			Write(" const");
	}

	void WriteDeclarations(CiClass klass, CiVisibility visibility, string visibilityKeyword)
	{
		bool constructor = GetConstructorVisibility(klass) == visibility;
		bool destructor = visibility == CiVisibility.Public && klass.AddsVirtualMethods();
		if (!constructor && !destructor && !HasMembersOfVisibility(klass, visibility))
			return;

		Write(visibilityKeyword);
		WriteLine(':');
		this.Indent++;

		if (constructor) {
			if (klass.Constructor != null)
				WriteDoc(klass.Constructor.Documentation);
			Write(klass.Name);
			Write("()");
			if (klass.CallType == CiCallType.Static)
				Write(" = delete");
			else if (klass.Constructor == null)
				Write(" = default");
			WriteLine(';');
		}

		if (destructor) {
			Write("virtual ~");
			Write(klass.Name);
			WriteLine("() = default;");
		}

		for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
			if (!(symbol is CiMember member) || member.Visibility != visibility)
				continue;
			switch (member) {
			case CiConst konst:
				WriteDoc(konst.Documentation);
				WriteConst(konst);
				break;
			case CiField field:
				WriteField(field);
				break;
			case CiMethod method:
				WriteMethodDoc(method);
				switch (method.CallType) {
				case CiCallType.Static:
					Write("static ");
					break;
				case CiCallType.Abstract:
				case CiCallType.Virtual:
					Write("virtual ");
					break;
				default:
					break;
				}
				WriteTypeAndName(method);
				WriteParametersAndConst(method, true);
				switch (method.CallType) {
				case CiCallType.Abstract:
					Write(" = 0");
					break;
				case CiCallType.Override:
					Write(" override");
					break;
				case CiCallType.Sealed:
					Write(" final");
					break;
				default:
					break;
				}
				WriteLine(';');
				break;
			default:
				throw new NotImplementedException(member.Type.ToString());
			}
		}

		this.Indent--;
	}

	protected override void WriteClass(CiClass klass)
	{
		WriteLine();
		WriteDoc(klass.Documentation);
		OpenClass(klass, klass.CallType == CiCallType.Sealed ? " final" : "", " : public ");
		this.Indent--;
		WriteDeclarations(klass, CiVisibility.Public, "public");
		WriteDeclarations(klass, CiVisibility.Protected, "protected");
		WriteDeclarations(klass, CiVisibility.Internal, "public");
		WriteDeclarations(klass, CiVisibility.Private, "private");
		WriteLine("};");
	}

	void WriteConstructor(CiClass klass)
	{
		if (klass.Constructor == null)
			return;
		this.SwitchesWithGoto.Clear();
		Write(klass.Name);
		Write("::");
		Write(klass.Name);
		WriteLine("()");
		OpenBlock();
		WriteConstructorBody(klass);
		CloseBlock();
	}

	protected override void WriteMethod(CiMethod method)
	{
		if (method.CallType == CiCallType.Abstract)
			return;
		this.SwitchesWithGoto.Clear();
		WriteLine();
		WriteType(method.Type, true);
		WriteChar(' ');
		Write(method.Parent.Name);
		Write("::");
		WriteCamelCase(method.Name);
		WriteParametersAndConst(method, false);
		WriteBody(method);
	}

	void WriteResources(Dictionary<string, byte[]> resources, bool define)
	{
		if (resources.Count == 0)
			return;
		WriteLine();
		WriteLine("namespace");
		OpenBlock();
		WriteLine("namespace CiResource");
		OpenBlock();
		foreach (string name in resources.Keys.OrderBy(k => k)) {
			if (!define)
				Write("extern ");
			Include("array");
			Include("cstdint");
			Write("const std::array<uint8_t, ");
			VisitLiteralLong(resources[name].Length);
			Write("> ");
			WriteResource(name, -1);
			if (define) {
				WriteLine(" = {");
				WriteChar('\t');
				WriteBytes(resources[name]);
				Write(" }");
			}
			WriteLine(';');
		}
		CloseBlock();
		CloseBlock();
	}

	public override void WriteProgram(CiProgram program)
	{
		this.WrittenClasses.Clear();
		string headerFile = Path.ChangeExtension(this.OutputFile, "hpp");
		SortedSet<string> headerIncludes = new SortedSet<string>();
		this.Includes = headerIncludes;
		this.UsingStringViewLiterals = false;
		this.HasEnumFlags = false;
		OpenStringWriter();
		OpenNamespace();
		for (CiSymbol type = program.First; type != null; type = type.Next) {
			if (type is CiEnum enu)
				WriteEnum(enu);
			else {
				Write("class ");
				Write(type.Name);
				WriteLine(';');
			}
		}
		foreach (CiClass klass in program.Classes)
			WriteClass(klass, program);
		CloseNamespace();

		CreateFile(headerFile);
		WriteLine("#pragma once");
		WriteIncludes();
		if (this.HasEnumFlags) {
			WriteLine("#define CI_ENUM_FLAG_OPERATORS(T) \\");
			WriteLine("\tinline constexpr T operator~(T a) { return static_cast<T>(~static_cast<std::underlying_type_t<T>>(a)); } \\");
			WriteLine("\tinline constexpr T operator&(T a, T b) { return static_cast<T>(static_cast<std::underlying_type_t<T>>(a) & static_cast<std::underlying_type_t<T>>(b)); } \\");
			WriteLine("\tinline constexpr T operator|(T a, T b) { return static_cast<T>(static_cast<std::underlying_type_t<T>>(a) | static_cast<std::underlying_type_t<T>>(b)); } \\");
			WriteLine("\tinline constexpr T operator^(T a, T b) { return static_cast<T>(static_cast<std::underlying_type_t<T>>(a) ^ static_cast<std::underlying_type_t<T>>(b)); } \\");
			WriteLine("\tinline constexpr T &operator&=(T &a, T b) { return (a = a & b); } \\");
			WriteLine("\tinline constexpr T &operator|=(T &a, T b) { return (a = a | b); } \\");
			WriteLine("\tinline constexpr T &operator^=(T &a, T b) { return (a = a ^ b); }");
		}
		CloseStringWriter();
		CloseFile();

		this.Includes = new SortedSet<string>();
		OpenStringWriter();
		WriteResources(program.Resources, false);
		OpenNamespace();
		foreach (CiClass klass in program.Classes) {
			WriteConstructor(klass);
			WriteMethods(klass);
		}
		WriteResources(program.Resources, true);
		CloseNamespace();

		CreateFile(this.OutputFile);
		WriteTopLevelNatives(program);
		this.Includes.ExceptWith(headerIncludes);
		WriteIncludes();
		Write("#include \"");
		Write(Path.GetFileName(headerFile));
		WriteLine("\"");
		if (this.UsingStringViewLiterals)
			WriteLine("using namespace std::string_view_literals;");
		CloseStringWriter();
		CloseFile();
	}
}

}
