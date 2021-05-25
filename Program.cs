using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;

namespace SinUnfuscator
{
    class Program
    {
        public static ModuleDefMD asm;
        public static string path;

        static void fixLengthCalls()
        {
            foreach (TypeDef type in asm.Types)
            {
                foreach (MethodDef methods in type.Methods)
                {
                    if (!methods.HasBody) { continue; }
                    for (int x = 0; x < methods.Body.Instructions.Count; x++)
                    {
                        Instruction inst = methods.Body.Instructions[x];
                        if (inst.OpCode.Equals(OpCodes.Call))
                        {
                            if (inst.Operand.ToString().Contains("System.String::get_Length"))
                            {
                                int newLen = inst.Operand.ToString().Length;
                                methods.Body.Instructions[x - 1].OpCode = OpCodes.Ldc_I4;
                                methods.Body.Instructions[x - 1].Operand = newLen;
                                methods.Body.Instructions.Remove(inst);
                            }
                        }
                    }
                }
            }
        }

        static void removeJunk()
        {
            asm.EntryPoint.DeclaringType.Name = "Entrypoint";
            for (int x_type = 0; x_type < asm.Types.Count; x_type++)
            {
                TypeDef type = asm.Types[x_type];
                if (type.IsGlobalModuleType) { type.Name = "<Module>"; }
                if (type.HasInterfaces)
                {
                    bool wasInterfaceObf = false;
                    foreach (InterfaceImpl intrface in type.Interfaces)
                    {
                        if (intrface.Interface == type)
                        {
                            asm.Types.RemoveAt(x_type);
                            x_type--;
                            wasInterfaceObf = true;
                            break;
                        }
                    }
                    if (wasInterfaceObf) { continue; }
                }
                for (int x_methods = 0; x_methods < type.Methods.Count; x_methods++)
                {
                    MethodDef methods = type.Methods[x_methods];
                    if (!methods.HasBody) { continue; }
                    methods.Body.KeepOldMaxStack = true;
                    for (int x_inst = 0; x_inst < methods.Body.Instructions.Count; x_inst++)
                    {
                        Instruction inst = methods.Body.Instructions[x_inst];
                        switch (inst.OpCode.Code)
                        {
                            case Code.Ret:
                                if (methods.Body.Instructions[x_inst - 1].OpCode.Equals(OpCodes.Ldc_I4) && methods.Body.Instructions[x_inst - 2].OpCode.Equals(OpCodes.Ldc_I4))
                                {
                                    type.Methods.RemoveAt(x_methods);
                                    x_methods--;
                                }
                                break;
                            case Code.Call:
                                if (inst.Operand.ToString().Contains("System.Convert::FromBase64String")) { methods.Name = "StringDecoder"; }
                                if (inst.Operand.ToString().Contains("<Module>::Initialize")) { methods.Name = ".cctor"; }
                                break;
                        }
                    }
                }
                if (!type.HasMethods) { asm.Types.RemoveAt(x_type); x_type--; }
            }
        }

        static void fixStrings()
        {
            foreach (TypeDef type in asm.Types)
            {
                foreach (MethodDef methods in type.Methods)
                {
                    if (!methods.HasBody) { continue; }
                    for (int x = 0; x < methods.Body.Instructions.Count; x++)
                    {
                        if (x + 1 >= methods.Body.Instructions.Count) { continue; }
                        Instruction inst = methods.Body.Instructions[x];
                        if (inst.OpCode.Equals(OpCodes.Ldstr) && methods.Body.Instructions[x + 1].OpCode.Equals(OpCodes.Call))
                        {
                            if (methods.Body.Instructions[x + 1].Operand.ToString().Contains("<Module>::StringDecoder"))
                            {
                                inst.Operand = Encoding.UTF8.GetString(Convert.FromBase64String(inst.Operand.ToString()));
                                methods.Body.Instructions.RemoveAt(x + 1);
                            }
                        }
                    }
                }
            }
        }

        static void disableProtections()
        {
            foreach (TypeDef type in asm.Types)
            {
                if (type.IsGlobalModuleType)
                {
                    type.Methods.Clear();
                }
            }
        }

        static void Main(string[] args)
        {
            Console.Title = "SinUnfuscator";
            Console.WriteLine();
            Console.WriteLine(" SinUnfuscator by misonothx - SaintFuscator Deobfuscator");
            Console.WriteLine("  |- https://github.com/miso-xyz/SinUnfuscator/");
            Console.WriteLine();
            path = args[0];
            asm = ModuleDefMD.Load(args[0]);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(" Removing Junk...");
            removeJunk();
            Console.WriteLine(" Decoding Strings...");
            fixStrings();
            Console.WriteLine(" Simplifying Length Calls...");
            fixLengthCalls();
            Console.WriteLine(" Removing Protections...");
            disableProtections();
            ModuleWriterOptions moduleWriterOptions = new ModuleWriterOptions(asm);
            moduleWriterOptions.MetadataOptions.Flags |= MetadataFlags.PreserveAll;
            moduleWriterOptions.Logger = DummyLogger.NoThrowInstance;
            NativeModuleWriterOptions nativeModuleWriterOptions = new NativeModuleWriterOptions(asm, true);
            nativeModuleWriterOptions.MetadataOptions.Flags |= MetadataFlags.PreserveAll;
            nativeModuleWriterOptions.Logger = DummyLogger.NoThrowInstance;
            if (asm.IsILOnly) { asm.Write(Path.GetFileNameWithoutExtension(path) + "-SinUnfuscator" + Path.GetExtension(path)); }
            else { asm.NativeWrite(Path.GetFileNameWithoutExtension(path) + "-SinUnfuscator" + Path.GetExtension(path)); }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(" Successfully cleaned! (saved as '" + Path.GetFileNameWithoutExtension(path) + "-SinUnfuscator" + Path.GetExtension(path) + "')");
            Console.ResetColor();
            Console.WriteLine(" Press any key to exit...");
            Console.ReadKey();
        }
    }
}