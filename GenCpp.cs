// GenCpp.cs - C++ code generator
//
// Copyright (C) 2011-2019  Piotr Fusik
//
// This file is part of CiTo, see http://cito.sourceforge.net
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

	protected override void IncludeStdInt()
	{
		Include("cstdint");
	}

	protected override void WriteLiteral(object value)
	{
		if (value == null)
			Write("nullptr");
		else
			base.WriteLiteral(value);
	}

	protected override void WriteName(CiSymbol symbol)
	{
		if (symbol is CiMember)
			WriteCamelCase(symbol.Name);
		else
			Write(symbol.Name);
	}

	public override CiExpr Visit(CiSymbolReference expr, CiPriority parent)
	{
		if (expr.Symbol is CiField)
			Write("this->");
		WriteName(expr.Symbol);
		return expr;
	}

	protected override void Write(CiType type, bool promote)
	{
		switch (type) {
		case null:
			Write("void");
			break;
		case CiIntegerType integer:
			Write(GetTypeCode(integer, promote));
			break;
		case CiStringPtrType _:
			Include("string_view");
			Write("std::string_view");
			break;
		case CiStringStorageType _:
			Include("string");
			Write("std::string");
			break;
		case CiArrayPtrType arrayPtr:
			switch (arrayPtr.Modifier) {
			case CiToken.EndOfFile:
				Write(arrayPtr.ElementType, false);
				Write(" const *");
				break;
			case CiToken.ExclamationMark:
				Write(arrayPtr.ElementType, false);
				Write(" *");
				break;
			case CiToken.Hash:
				Include("memory");
				Write("std::shared_ptr<");
				Write(arrayPtr.ElementType, false);
				Write("[]>");
				break;
			default:
				throw new NotImplementedException(arrayPtr.Modifier.ToString());
			}
			break;
		case CiArrayStorageType arrayStorage:
			Include("array");
			Write("std::array<");
			Write(arrayStorage.ElementType, false);
			Write(", ");
			Write(arrayStorage.Length);
			Write('>');
			break;
		case CiClassPtrType classPtr:
			switch (classPtr.Modifier) {
			case CiToken.EndOfFile:
				Write("const ");
				Write(classPtr.Class.Name);
				Write(" *");
				break;
			case CiToken.ExclamationMark:
				Write(classPtr.Class.Name);
				Write(" *");
				break;
			case CiToken.Hash:
				Include("memory");
				Write("std::shared_ptr<");
				Write(classPtr.Class.Name);
				Write('>');
				break;
			default:
				throw new NotImplementedException(classPtr.Modifier.ToString());
			}
			break;
		default:
			Write(type.Name);
			break;
		}
	}

	protected override void WriteNew(CiClass klass)
	{
		Include("memory");
		Write("std::make_shared<");
		Write(klass.Name);
		Write(">()");
	}

	protected override void WriteArrayStorageInit(CiArrayStorageType array, CiExpr value)
	{
		switch (value) {
		case null:
			break;
		case CiLiteral literal when literal.IsDefaultValue:
			Write(" {}");
			break;
		default:
			throw new NotImplementedException("Only null, zero and false supported");
		}
	}

	protected override void WriteVarInit(CiNamedValue def)
	{
		if (def.Value != null && def.Type == CiSystem.StringStorageType) {
			Write('{');
			def.Value.Accept(this, CiPriority.Statement);
			Write('}');
		}
		else
			base.WriteVarInit(def);
	}

	protected override bool HasInitCode(CiNamedValue def)
	{
		return false;
	}

	protected override void WriteInitCode(CiNamedValue def)
	{
	}

	protected override void WriteEqual(CiBinaryExpr expr, CiPriority parent, bool not)
	{
		if (expr.Left.Type == CiSystem.StringPtrType && expr.Right.Type == CiSystem.NullType) {
			WriteCoerced(CiSystem.StringPtrType, expr.Left, CiPriority.Primary);
			Write(".data() ");
			Write(not ? '!' : '=');
			Write("= nullptr");
		}
		else if (expr.Left.Type == CiSystem.NullType && expr.Right.Type == CiSystem.StringPtrType) {
			Write("nullptr ");
			Write(not ? '!' : '=');
			Write("= ");
			WriteCoerced(CiSystem.StringPtrType, expr.Right, CiPriority.Primary);
			Write(".data() ");
		}
		else
			base.WriteEqual(expr, parent, not);
	}

	protected override void WriteMemberOp(CiExpr left, CiSymbolReference symbol)
	{
		if (symbol.Symbol is CiConst) // FIXME
			Write("::");
		else if (left.Type is CiClassPtrType)
			Write("->");
		else
			Write('.');
	}

	void WriteStringMethod(CiExpr obj, string name, CiMethod method, CiExpr[] args)
	{
		obj.Accept(this, CiPriority.Primary);
		if (obj is CiLiteral) {
			this.UsingStringViewLiterals = true;
			Write("sv");
		}
		Write('.');
		Write(name);
		WriteArgsInParentheses(method, args);
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, CiExpr[] args, CiPriority parent)
	{
		if (IsMathReference(obj)) {
			Include("cmath");
			Write("std::");
			if (method.Name == "Ceiling")
				Write("ceil");
			else
				WriteLowercase(method.Name);
			WriteArgsInParentheses(method, args);
		}
		else if (method == CiSystem.StringContains) {
			if (parent > CiPriority.Equality)
				Write('(');
			WriteStringMethod(obj, "find", method, args);
			Write(" != std::string::npos");
			if (parent > CiPriority.Equality)
				Write(')');
		}
		else if (method == CiSystem.StringIndexOf) {
			Write("static_cast<int>(");
			WriteStringMethod(obj, "find", method, args);
			Write(')');
		}
		else if (method == CiSystem.StringLastIndexOf) {
			Write("static_cast<int>(");
			WriteStringMethod(obj, "rfind", method, args);
			Write(')');
		}
		else if (method == CiSystem.StringStartsWith)
			WriteStringMethod(obj, "starts_with", method, args);
		else if (method == CiSystem.StringEndsWith)
			WriteStringMethod(obj, "ends_with", method, args);
		else if (method == CiSystem.StringSubstring)
			WriteStringMethod(obj, "substr", method, args);
		else if (obj.Type is CiArrayType && method.Name == "CopyTo") {
			Include("algorithm");
			Write("std::copy_n(");
			WriteArrayPtrAdd(obj, args[0]);
			Write(", ");
			args[3].Accept(this, CiPriority.Statement);
			Write(", ");
			WriteArrayPtrAdd(args[1], args[2]);
			Write(')');
		}
		else if (method == CiSystem.UTF8GetString) {
			Include("string_view");
			Write("std::string_view(reinterpret_cast<const char *>(");
			WriteArrayPtrAdd(args[0], args[1]);
			Write("), ");
			args[2].Accept(this, CiPriority.Statement);
			Write(')');
		}
		else {
			obj.Accept(this, CiPriority.Primary);
			if (method.CallType == CiCallType.Static)
				Write("::");
			else if (obj.Type is CiClassPtrType)
				Write("->");
			else
				Write('.');
			WriteCamelCase(method.Name);
			WriteArgsInParentheses(method, args);
		}
	}

	protected override void WriteResource(string name, int length)
	{
		if (length >= 0) // reference as opposed to definition
			Write("CiResource::");
		foreach (char c in name)
			Write(CiLexer.IsLetterOrDigit(c) ? c : '_');
	}

	protected override void WriteArrayPtr(CiExpr expr, CiPriority parent)
	{
		switch (expr.Type) {
		case CiArrayStorageType _:
			expr.Accept(this, CiPriority.Primary);
			Write(".data()");
			break;
		case CiArrayPtrType arrayPtr when arrayPtr.Modifier == CiToken.Hash:
			expr.Accept(this, CiPriority.Primary);
			Write(".get()");
			break;
		default:
			expr.Accept(this, parent);
			break;
		}
	}

	protected override void WriteCoercedInternal(CiType type, CiExpr expr, CiPriority parent)
	{
		switch (type) {
		case CiClassPtrType leftClass when leftClass.Modifier != CiToken.Hash:
			switch (expr.Type) {
			case CiClass _:
				Write('&');
				expr.Accept(this, CiPriority.Primary);
				return;
			case CiClassPtrType rightPtr when rightPtr.Modifier == CiToken.Hash:
				expr.Accept(this, CiPriority.Primary);
				Write(".get()");
				return;
			default:
				break;
			}
			break;
		case CiArrayPtrType leftArray when leftArray.Modifier != CiToken.Hash:
			WriteArrayPtr(expr, CiPriority.Statement);
			return;
		case CiStringPtrType _ when expr.Type == CiSystem.NullType:
			Include("string_view");
			Write("std::string_view(nullptr, 0)");
			return;
		default:
			break;
		}
		base.WriteCoercedInternal(type, expr, parent);
	}

	protected override void WriteStringLength(CiExpr expr)
	{
		expr.Accept(this, CiPriority.Primary);
		Write(".length()");
	}

	public override CiExpr Visit(CiBinaryExpr expr, CiPriority parent)
	{
		if (expr.Left.Type == CiSystem.StringStorageType
		 && expr.Op == CiToken.Assign
		 && parent == CiPriority.Statement) {
			CiExpr length = IsTrimSubstring(expr);
			if (length != null) {
				expr.Left.Accept(this, CiPriority.Primary);
				Write(".resize(");
				length.Accept(this, CiPriority.Statement);
				Write(')');
				return expr;
			}
		}
		return base.Visit(expr, parent);
	}

	protected override void WriteConst(CiConst konst)
	{
		Write("static constexpr ");
		WriteTypeAndName(konst);
		Write(" = ");
		konst.Value.Accept(this, CiPriority.Statement);
		WriteLine(';');
	}

	protected override void WriteCaseBody(CiStatement[] statements)
	{
		bool block = false;
		foreach (CiStatement statement in statements) {
			if (!block && statement is CiVar) {
				OpenBlock();
				block = true;
			}
			statement.Accept(this);
		}
		if (block)
			CloseBlock();
	}

	public override void Visit(CiThrow statement)
	{
		Include("exception");
		WriteLine("throw std::exception();");
		// TODO: statement.Message.Accept(this, CiPriority.Statement);
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

	void Write(CiEnum enu)
	{
		WriteLine();
		Write("enum class ");
		WriteLine(enu.Name);
		OpenBlock();
		bool first = true;
		foreach (CiConst konst in enu) {
			if (!first)
				WriteLine(',');
			first = false;
			WriteCamelCase(konst.Name);
			if (konst.Value != null) {
				Write(" = ");
				konst.Value.Accept(this, CiPriority.Statement);
			}
		}
		WriteLine();
		this.Indent--;
		WriteLine("};");
	}

	CiVisibility GetConstructorVisibility(CiClass klass)
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

	void WriteParametersAndConst(CiMethod method)
	{
		WriteParameters(method);
		if (method.CallType != CiCallType.Static && !method.IsMutator)
			Write(" const");
	}

	void WriteDeclarations(CiClass klass, CiVisibility visibility, string visibilityKeyword)
	{
		bool constructor = GetConstructorVisibility(klass) == visibility;
		IEnumerable<CiConst> consts = klass.Consts.Where(c => c.Visibility == visibility);
		IEnumerable<CiField> fields = klass.Fields.Where(f => f.Visibility == visibility);
		IEnumerable<CiMethod> methods = klass.Methods.Where(m => m.Visibility == visibility);
		if (!constructor && !consts.Any() && !fields.Any() && !methods.Any())
			return;

		Write(visibilityKeyword);
		WriteLine(':');
		this.Indent++;

		if (constructor) {
			Write(klass.Name);
			Write("()");
			if (klass.CallType == CiCallType.Static)
				Write(" = delete");
			else if (klass.Constructor == null)
				Write(" = default");
			WriteLine(';');
		}

		foreach (CiConst konst in consts)
			WriteConst(konst);

		foreach (CiField field in fields)
		{
			WriteVar(field);
			WriteLine(';');
		}

		foreach (CiMethod method in methods)
		{
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
			WriteParametersAndConst(method);
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
		}

		this.Indent--;
	}

	void Write(CiClass klass)
	{
		// topological sorting of class hierarchy and class storage fields
		if (klass == null)
			return;
		if (this.WrittenClasses.TryGetValue(klass, out bool done)) {
			if (done)
				return;
			throw new CiException(klass, "Circular dependency for class {0}", klass.Name);
		}
		this.WrittenClasses.Add(klass, false);
		Write(klass.Parent as CiClass);
		foreach (CiField field in klass.Fields)
			Write(field.Type.BaseType as CiClass);
		this.WrittenClasses[klass] = true;

		WriteLine();
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
		Write(klass.Name);
		Write("::");
		Write(klass.Name);
		WriteLine("()");
		OpenBlock();
		Write(((CiBlock) klass.Constructor.Body).Statements);
		CloseBlock();
	}

	void WriteMethod(CiClass klass, CiMethod method)
	{
		if (method.CallType == CiCallType.Abstract)
			return;
		WriteLine();
		Write(method.Type, true);
		Write(' ');
		Write(klass.Name);
		Write("::");
		WriteCamelCase(method.Name);
		WriteParametersAndConst(method);
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
			Write(resources[name].Length);
			Write("> ");
			WriteResource(name, -1);
			if (define) {
				WriteLine(" = {");
				Write('\t');
				Write(resources[name]);
				Write(" }");
			}
			WriteLine(';');
		}
		CloseBlock();
		CloseBlock();
	}

	public override void Write(CiProgram program)
	{
		this.WrittenClasses.Clear();
		string headerFile = Path.ChangeExtension(this.OutputFile, "hpp");
		SortedSet<string> headerIncludes = new SortedSet<string>();
		this.Includes = headerIncludes;
		OpenStringWriter();
		OpenNamespace();
		foreach (CiEnum enu in program.OfType<CiEnum>())
			Write(enu);
		foreach (CiClass klass in program.Classes) {
			Write("class ");
			Write(klass.Name);
			WriteLine(';');
		}
		foreach (CiClass klass in program.Classes)
			Write(klass);
		CloseNamespace();

		CreateFile(headerFile);
		WriteLine("#pragma once");
		WriteIncludes();
		CloseStringWriter();
		CloseFile();

		this.Includes = new SortedSet<string>();
		OpenStringWriter();
		WriteResources(program.Resources, false);
		OpenNamespace();
		foreach (CiClass klass in program.Classes) {
			WriteConstructor(klass);
			foreach (CiMethod method in klass.Methods)
				WriteMethod(klass, method);
		}
		WriteResources(program.Resources, true);
		CloseNamespace();

		CreateFile(this.OutputFile);
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
