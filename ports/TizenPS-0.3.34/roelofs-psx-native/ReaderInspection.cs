using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;

// Read metadata/IL only: never invoke controller methods, read static field values,
// load another native library, or run a type initializer for this inspection.
internal static class ReaderInspection {
 static readonly Dictionary<short,OpCode> Codes=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static)
  .Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)).ToDictionary(o=>o.Value);
 internal static string Capture(Type reader){
  var text=new StringBuilder();
  try{
   text.AppendLine("Controller reader implementation (read-only inspection)");
   text.AppendLine("Assembly: "+reader.Assembly.FullName);
   var queue=new Queue<MethodBase>();
   foreach(var m in reader.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.DeclaredOnly))
    if(m.Name=="Initialise"||m.Name=="NormalizeAxis"||m.Name=="AddDevice"||m.Name=="RemoveDevice")queue.Enqueue(m);
   var native=reader.Assembly.GetType("Interop+GamepadClient");
   if(native!=null)foreach(var m in native.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.DeclaredOnly))queue.Enqueue(m);var visited=new HashSet<string>();var types=new HashSet<Type>();
   foreach(var m in reader.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.DeclaredOnly))
    if(m.Name=="GetState"||m.Name=="GetCapabilities")queue.Enqueue(m);
   if(reader.TypeInitializer!=null)queue.Enqueue(reader.TypeInitializer);
   while(queue.Count>0&&visited.Count<80&&text.Length<110000){
    var method=queue.Dequeue();string id=method.Module.ModuleVersionId+":"+method.MetadataToken;
    if(!visited.Add(id))continue;
    var type=method.DeclaringType;
    if(types.Add(type)){
     text.AppendLine("TYPE "+type.FullName+" | "+type.Attributes);
     foreach(var field in type.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance|BindingFlags.DeclaredOnly)){
      text.AppendLine(" FIELD "+field.Attributes+" "+field.FieldType+" "+field.Name+(field.IsLiteral?" = "+field.GetRawConstantValue():""));
      var marshal=field.GetCustomAttribute<MarshalAsAttribute>();if(marshal!=null)text.AppendLine("  MARSHAL "+marshal.Value+" size="+marshal.SizeConst);
     }
    }
    text.AppendLine("METHOD "+type.FullName+"::"+method+" | "+method.Attributes);
    var import=method.GetCustomAttribute<DllImportAttribute>();
    if(import!=null){text.AppendLine(" IMPORT "+import.Value+"::"+import.EntryPoint+" convention="+import.CallingConvention);continue;}
    MethodBody body;try{body=method.GetMethodBody();}catch(Exception e){text.AppendLine(" BODY UNAVAILABLE: "+e.GetType().Name);continue;}
    if(body==null){text.AppendLine(" NO MANAGED BODY");continue;}
    foreach(var local in body.LocalVariables)text.AppendLine(" LOCAL "+local.LocalIndex+": "+local.LocalType);
    var bytes=body.GetILAsByteArray();int offset=0,instructions=0;
    while(offset<bytes.Length&&instructions++<1200){
     int start=offset;short code=bytes[offset++];if(code==0xfe){Require(bytes,offset,1);code=unchecked((short)(0xfe00|bytes[offset++]));}
     if(!Codes.TryGetValue(code,out OpCode op)){text.AppendLine(" UNKNOWN OPCODE "+code);break;}
     string operand="";int size=OperandSize(op.OperandType,bytes,offset);Require(bytes,offset,size);
     switch(op.OperandType){
      case OperandType.ShortInlineI:operand=unchecked((sbyte)bytes[offset]).ToString();break;
      case OperandType.InlineI:operand=BitConverter.ToInt32(bytes,offset).ToString();break;
      case OperandType.InlineI8:operand=BitConverter.ToInt64(bytes,offset).ToString();break;
      case OperandType.ShortInlineR:operand=BitConverter.ToSingle(bytes,offset).ToString("R",CultureInfo.InvariantCulture);break;
      case OperandType.InlineR:operand=BitConverter.ToDouble(bytes,offset).ToString("R",CultureInfo.InvariantCulture);break;
      case OperandType.ShortInlineVar:operand=bytes[offset].ToString();break;
      case OperandType.InlineVar:operand=BitConverter.ToUInt16(bytes,offset).ToString();break;
      case OperandType.ShortInlineBrTarget:operand="IL_"+(offset+size+unchecked((sbyte)bytes[offset])).ToString("X4");break;
      case OperandType.InlineBrTarget:operand="IL_"+(offset+size+BitConverter.ToInt32(bytes,offset)).ToString("X4");break;
      case OperandType.InlineSwitch:var targets=new List<string>();for(int j=0;j<BitConverter.ToInt32(bytes,offset);j++)targets.Add("IL_"+(offset+size+BitConverter.ToInt32(bytes,offset+4+j*4)).ToString("X4"));operand=string.Join(", ",targets);break;
      case OperandType.InlineString:
       try{string literal=method.Module.ResolveString(BitConverter.ToInt32(bytes,offset));operand="\""+literal.Substring(0,Math.Min(160,literal.Length)).Replace("\r","\\r").Replace("\n","\\n")+"\"";}catch{operand="unresolved string";}break;
      case OperandType.InlineField:case OperandType.InlineMethod:case OperandType.InlineType:case OperandType.InlineTok:
       try{
        var member=method.Module.ResolveMember(BitConverter.ToInt32(bytes,offset));operand=(member.DeclaringType==null?"":member.DeclaringType.FullName+"::")+member;
        if(member is MethodBase called&&called.DeclaringType.Assembly==reader.Assembly){queue.Enqueue(called);if(called.DeclaringType.TypeInitializer!=null)queue.Enqueue(called.DeclaringType.TypeInitializer);}
       }catch(Exception e){operand="unresolved token ("+e.GetType().Name+")";}break;
      case OperandType.InlineSig:operand="signature 0x"+BitConverter.ToInt32(bytes,offset).ToString("X8");break;
     }
     text.AppendLine(" IL_"+start.ToString("X4")+": "+op.Name+" "+operand);offset+=size;
    }
    if(offset<bytes.Length)text.AppendLine(" INSTRUCTION LIMIT REACHED");
    foreach(var clause in body.ExceptionHandlingClauses)text.AppendLine(" EXCEPTION "+clause.Flags+" try="+clause.TryOffset+"+"+clause.TryLength+" handler="+clause.HandlerOffset+"+"+clause.HandlerLength);
   }
   if(queue.Count>0)text.AppendLine("INSPECTION LIMIT REACHED; further methods omitted.");
  }catch(Exception e){text.AppendLine("Inspection unavailable: "+e.GetType().Name+": "+e.Message);}
  return text.ToString();
 }
 static void Require(byte[] bytes,int offset,int count){if(offset<0||count<0||offset>bytes.Length-count)throw new InvalidOperationException("Truncated IL operand");}
 static int OperandSize(OperandType type,byte[] bytes,int offset){
  switch(type){case OperandType.InlineNone:return 0;case OperandType.ShortInlineI:case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineVar:return 1;
   case OperandType.InlineVar:return 2;case OperandType.InlineI8:case OperandType.InlineR:return 8;
   case OperandType.InlineSwitch:Require(bytes,offset,4);int n=BitConverter.ToInt32(bytes,offset);if(n<0||n>(bytes.Length-offset-4)/4)throw new InvalidOperationException("Invalid IL switch");return 4+n*4;
   default:return 4;
  }
 }
}
