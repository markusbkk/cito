// GenTs.cs - TypeScript code generator
//
// Copyright (C) 2020-2021  Andy Edwards
// Copyright (C) 2020-2022  Piotr Fusik
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

namespace Foxoft.Ci
{

public class GenTs : GenJs
{
	// GenFullCode = false: only generate TypeScript declarations (.d.ts files)
	// GenFullCode = true: generate full TypeScript code
	bool GenFullCode = false;

	public GenTs WithGenFullCode()
	{
		GenFullCode = true;
		return this;
	}

	protected override bool IsJsPrivate(CiMember member) => false;

	public override void VisitEnumValue(CiConst konst, CiConst previous)
	{
		WriteEnumValue(konst);
		WriteLine(',');
	}

	protected override void WriteEnum(CiEnum enu)
	{
		// WARNING: TypeScript enums allow reverse lookup that the Js generator currently
		// doesn't implement
		// https://www.typescriptlang.org/docs/handbook/enums.html#reverse-mappings
		WriteLine();
		WriteDoc(enu.Documentation);
		Write("export enum ");
		Write(enu.Name);
		WriteChar(' ');
		OpenBlock();
		enu.AcceptValues(this);
		WriteLine();
		CloseBlock();
	}

	protected override void WriteTypeAndName(CiNamedValue value)
	{
		WriteName(value);
		Write(": ");
		WriteType(value.Type);
	}

	void WriteType(CiType type, bool readOnly = false)
	{
		switch (type) {
		case CiNumericType _:
			Write("number");
			break;
		case CiEnum enu:
			Write(enu == CiSystem.BoolType ? "boolean" : enu.Name);
			break;
		case CiClassType klass:
			readOnly |= !(klass is CiReadWriteClassType);
			switch (klass.Class.Id) {
			case CiId.StringClass:
				Write("string");
				break;
			case CiId.ArrayPtrClass when !(klass.GetElementType() is CiNumericType):
			case CiId.ArrayStorageClass when !(klass.GetElementType() is CiNumericType):
			case CiId.ListClass:
			case CiId.QueueClass:
			case CiId.StackClass:
				if (readOnly)
					Write("readonly ");
				if (klass.GetElementType().IsNullable())
					WriteChar('(');
				WriteType(klass.GetElementType());
				if (klass.GetElementType().IsNullable())
					WriteChar(')');
				Write("[]");
				break;
			default:
				if (readOnly && klass.Class.TypeParameterCount > 0)
					Write("Readonly<");
				switch (klass.Class.Id) {
				case CiId.ArrayPtrClass:
				case CiId.ArrayStorageClass:
					Write(GetArrayElementType((CiNumericType) klass.GetElementType()));
					Write("Array");
					break;
				case CiId.HashSetClass:
					Write("Set<");
					WriteType(klass.GetElementType(), false);
					WriteChar('>');
					break;
				case CiId.DictionaryClass:
				case CiId.SortedDictionaryClass:
					if (klass.GetKeyType() is CiEnum)
						Write("Partial<");
					Write("Record<");
					WriteType(klass.GetKeyType());
					Write(", ");
					WriteType(klass.GetValueType());
					WriteChar('>');
					if (klass.GetKeyType() is CiEnum)
						WriteChar('>');
					break;
				case CiId.OrderedDictionaryClass:
					Write("Map<");
					WriteType(klass.GetKeyType());
					Write(", ");
					WriteType(klass.GetValueType());
					WriteChar('>');
					break;
				case CiId.RegexClass:
					Write("RegExp");
					break;
				case CiId.MatchClass:
					Write("RegExpMatchArray");
					break;
				default:
					Write(klass.Class.Name);
					break;
				}
				if (readOnly && klass.Class.TypeParameterCount > 0)
					WriteChar('>');
				break;
			}
			if (type.IsNullable())
				Write(" | null");
			break;
		default:
			Write(type.Name);
			break;
		}
	}

	protected override void WriteAssertCastType(CiVar def)
	{
		Write(" as ");
		Write(def.Type.Name);
	}

	void WriteVisibility(CiVisibility visibility)
	{
		switch (visibility) {
		case CiVisibility.Private:
			Write("private ");
			break;
		case CiVisibility.Internal:
			break;
		case CiVisibility.Protected:
			Write("protected ");
			break;
		case CiVisibility.Public:
			Write("public ");
			break;
		}
	}

	protected override void WriteConst(CiConst konst)
	{
		WriteLine();
		WriteDoc(konst.Documentation);
		WriteVisibility(konst.Visibility);
		Write("static readonly ");
		WriteName(konst);
		Write(": ");
		WriteType(konst.Type, true);
		if (this.GenFullCode)
			WriteVarInit(konst);
		WriteLine(';');
	}

	protected override void WriteField(CiField field)
	{
		WriteDoc(field.Documentation);
		WriteVisibility(field.Visibility);
		if (field.Type.IsFinal() && !field.IsAssignableStorage())
			Write("readonly ");
		WriteTypeAndName(field);
		if (this.GenFullCode)
			WriteVarInit(field);
		WriteLine(';');
	}

	protected override void WriteMethod(CiMethod method)
	{
		WriteLine();
		WriteMethodDoc(method);
		WriteVisibility(method.Visibility);
		switch (method.CallType) {
		case CiCallType.Static:
			Write("static ");
			break;
		case CiCallType.Virtual:
			break;
		case CiCallType.Abstract:
			Write("abstract ");
			break;
		case CiCallType.Override:
			break;
		case CiCallType.Normal:
			// no final methods in TS
			break;
		case CiCallType.Sealed:
			// no final methods in TS
			break;
		default:
			throw new NotImplementedException(method.CallType.ToString());
		}
		WriteName(method);
		WriteChar('(');
		int i = 0;
		for (CiVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
			if (i > 0)
				Write(", ");
			WriteName(param);
			if (param.Value != null && !this.GenFullCode)
				WriteChar('?');
			Write(": ");
			WriteType(param.Type);
			if (param.Value != null && this.GenFullCode)
				WriteVarInit(param);
			i++;
		}
		Write("): ");
		WriteType(method.Type);
		if (this.GenFullCode)
			WriteBody(method);
		else
			WriteLine(';');
	}

	protected override void WriteClass(CiClass klass, CiProgram program)
	{
		if (!WriteBaseClass(klass, program))
			return;

		WriteLine();
		WriteDoc(klass.Documentation);
		Write("export ");
		switch (klass.CallType) {
		case CiCallType.Normal:
			break;
		case CiCallType.Abstract:
			Write("abstract ");
			break;
		case CiCallType.Static:
		case CiCallType.Sealed:
			// there's no final/sealed keyword, but we accomplish it by marking the constructor private
			break;
		default:
			throw new NotImplementedException(klass.CallType.ToString());
		}
		OpenClass(klass, "", " extends ");

		if (NeedsConstructor(klass) || klass.CallType == CiCallType.Static) {
			if (klass.Constructor != null) {
				WriteDoc(klass.Constructor.Documentation);
				WriteVisibility(klass.Constructor.Visibility);
			}
			else if (klass.CallType == CiCallType.Static)
				Write("private ");
			if (this.GenFullCode)
				WriteConstructor(klass);
			else
				WriteLine("constructor();");
		}

		WriteMembers(klass, this.GenFullCode);
		CloseBlock();
	}

	public override void WriteProgram(CiProgram program)
	{
		CreateFile(this.OutputFile);
		if (this.GenFullCode)
			WriteTopLevelNatives(program);
		WriteTypes(program);
		if (this.GenFullCode)
			WriteLib(program.Resources);
		CloseFile();
	}
}

}
