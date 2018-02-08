using Mono.Cecil;
using System.Reflection;
using System.Collections.Generic;
using Mono.Cecil.Cil;
using System;
using System.Reflection.Emit;
using System.Linq;

namespace Celeste.Mod {
    static class MethodManipulator {
        public static readonly AssemblyDefinition CelesteAssemblyDefinition = AssemblyDefinition.ReadAssembly("Celeste.exe");
        
        public static MethodCecilAndEmit getCelesteMethodDefinition(MethodInfo method) {
            var module = method.Module;
            var moduleDefinition = CelesteAssemblyDefinition.Modules.Single((e) => e.FileName == module.FullyQualifiedName);
            
            var type = method.DeclaringType;
            var typeDefinition = moduleDefinition.Types.Single((e) => e.FullName == type.FullName);
            
            var methodDefinition = typeDefinition.Methods.Where((e) => e.Name == method.Name).Where((e) => e.ReturnType.FullName == method.ReturnType.FullName).ElementAt(0); // TODO: Compare parameters as well (don't know how to do this easily)

            return new MethodCecilAndEmit(methodDefinition, method);
        }
        
        public static List<MethodCecilAndEmit> methodCalls(MethodDefinition method) {
            var refs = new List<MethodCecilAndEmit>();
            
            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.OpCode == Mono.Cecil.Cil.OpCodes.Call)
                {
                    MethodReference methodCall = instruction.Operand as MethodReference;
                    refs.Add(methodCall);
                }
            }
            
            return refs;
        }
        
        public class MethodModifier {
            public MethodCecilAndEmit Method;
            
            public List<MethodCecilAndEmit> CalledMethods;
            
            public MethodModifier(MethodCecilAndEmit method) {
                Method = method;
                if (Method.Definition == null) Method = getCelesteMethodDefinition(method);
                CalledMethods = methodCalls(method);
            }
            
            public class HookableMethod : MethodCecilAndEmit {
                public List<dynamic> FakedParameters;

                public MethodModifier Modifier;

                public HookableMethod(MethodDefinition def, MethodInfo info) : base(def, info)
                {
                }

                public HookableMethod(MethodDefinition def, MethodReference refer, MethodInfo info) : base(def, refer, info)
                {
                }

                public HookableMethod(MethodReference refer, MethodInfo info) : base(refer, info)
                {
                }

                public delegate dynamic Detour(HookableMethod hooks, dynamic orig, params dynamic[] args);
                
                public void Hook(Detour detour = null)
                {
                    if (detour == null) detour = (hooks, orig, args) => orig();

                    var type = delegateFactory.CreateDelegateType(this);
                    /*Type[] types = Info.GetParameters().Select((e) => e.ParameterType) as Type[];

                    var handler = new DynamicMethod(String.Format("hook_{0}", Info.Name), Info.ReturnType, types);

                    var emitter = handler.GetILGenerator();

                    for (short i = 0; i < types.Length; i++)
                    {
                        emitter.Emit(System.Reflection.Emit.OpCodes.Ldarg, i);
                    }

                    emitter.EmitCall(System.Reflection.Emit.OpCodes.Call, detour.Method, null);
                    emitter.Emit(System.Reflection.Emit.OpCodes.Ret);*/ // someone can fix this if they want, but it won't be me

                    dynamic trampoline = null;

                    MethodBase wrapper;

                    switch (Info.GetParameters().Length)
                    {
                        case 1:
                            Func<dynamic, dynamic> lamb1 = (a) => detour(this, trampoline, a);
                            wrapper = lamb1.Method;
                            break;
                        case 2:
                            Func<dynamic, dynamic, dynamic> lamb2 = (a, b) => detour(this, trampoline, a, b);
                            wrapper = lamb2.Method;
                            break;
                    }
                }
            }
        }

        public class MethodCecilAndEmit
        {
            public MethodDefinition Definition;
            public MethodReference Reference;
            public MethodInfo Info;

            public MethodCecilAndEmit(MethodDefinition def, MethodInfo info)
            {
                Definition = def;
                Info = info;
            }

            public MethodCecilAndEmit(MethodDefinition def, MethodReference refer, MethodInfo info)
            {
                Definition = def;
                Info = info;
                Reference = refer;
            }

            public MethodCecilAndEmit(MethodReference refer, MethodInfo info)
            {
                Info = info;
                Reference = refer;
            }

            public static implicit operator MethodDefinition(MethodCecilAndEmit self)
            {
                return self.Definition;
            }

            public static implicit operator MethodInfo(MethodCecilAndEmit self)
            {
                return self.Info;
            }

            public static implicit operator MethodReference(MethodCecilAndEmit self)
            {
                return self.Reference;
            }

            public static implicit operator MethodCecilAndEmit(MethodDefinition def)
            {
                return new MethodCecilAndEmit(def, null);
            }

            public static implicit operator MethodCecilAndEmit(MethodInfo info)
            {
                return new MethodCecilAndEmit(null, info);
            }

            public static implicit operator MethodCecilAndEmit(MethodReference refer)
            {
                return new MethodCecilAndEmit(refer, null);
            }
        }

        static DelegateTypeFactory delegateFactory = new DelegateTypeFactory();
        
        class DelegateTypeFactory // From https://stackoverflow.com/questions/9505117/creating-delegates-dynamically-with-parameter-names
        {
            private readonly ModuleBuilder m_module;

            public DelegateTypeFactory()
            {
                var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(
                    new AssemblyName("DelegateTypeFactory"), AssemblyBuilderAccess.RunAndCollect);
                m_module = assembly.DefineDynamicModule("DelegateTypeFactory");
            }

            public Type CreateDelegateType(MethodInfo method)
            {
                string nameBase = string.Format("{0}{1}", method.DeclaringType.Name, method.Name);
                string name = GetUniqueName(nameBase);

                var typeBuilder = m_module.DefineType(
                    name, System.Reflection.TypeAttributes.Sealed | System.Reflection.TypeAttributes.Public, typeof(MulticastDelegate));

                var constructor = typeBuilder.DefineConstructor(
                    System.Reflection.MethodAttributes.RTSpecialName | System.Reflection.MethodAttributes.HideBySig | System.Reflection.MethodAttributes.Public,
                    CallingConventions.Standard, new[] { typeof(object), typeof(IntPtr) });
                constructor.SetImplementationFlags(System.Reflection.MethodImplAttributes.CodeTypeMask);

                var parameters = method.GetParameters();

                var invokeMethod = typeBuilder.DefineMethod(
                    "Invoke", System.Reflection.MethodAttributes.HideBySig | System.Reflection.MethodAttributes.Virtual | System.Reflection.MethodAttributes.Public,
                    method.ReturnType, parameters.Select(p => p.ParameterType).ToArray());
                invokeMethod.SetImplementationFlags(System.Reflection.MethodImplAttributes.CodeTypeMask);

                for (int i = 0; i < parameters.Length; i++)
                {
                    var parameter = parameters[i];
                    invokeMethod.DefineParameter(i + 1, System.Reflection.ParameterAttributes.None, parameter.Name);
                }

                return typeBuilder.CreateType();
            }

            private string GetUniqueName(string nameBase)
            {
                int number = 2;
                string name = nameBase;
                while (m_module.GetType(name) != null)
                    name = nameBase + number++;
                return name;
            }
        }
    }

    // NOTE: The following code is from https://gist.github.com/waf/280152ab42aa92a85b79d6dbc812e68a, and makes writing detours with HookableMethod much easier.
    // However, the code does not have a license. I doubt it matters, as the code is extremely simple, but IANAL.

    /// <summary>
    /// Allow the up to the first eight elements of an array to take part in C# 7's destructuring syntax.
    /// </summary>
    /// <example>
    /// (int first, _, int middle, _, int[] rest) = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
    /// var (first, second, rest) = new[] { 1, 2, 3, 4 };
    /// </example>
    public static class ArrayDeconstructionExtensions
    {
        public static void Deconstruct<T>(this T[] array, out T first, out T[] rest)
        {
            first = array[0];
            rest = GetRestOfArray(array, 1);
        }
        public static void Deconstruct<T>(this T[] array, out T first, out T second, out T[] rest)
        {
            first = array[0];
            second = array[1];
            rest = GetRestOfArray(array, 2);
        }
        public static void Deconstruct<T>(this T[] array, out T first, out T second, out T third, out T[] rest)
        {
            first = array[0];
            second = array[1];
            third = array[2];
            rest = GetRestOfArray(array, 3);
        }
        public static void Deconstruct<T>(this T[] array, out T first, out T second, out T third, out T fourth, out T[] rest)
        {
            first = array[0];
            second = array[1];
            third = array[2];
            fourth = array[3];
            rest = GetRestOfArray(array, 4);
        }
        public static void Deconstruct<T>(this T[] array, out T first, out T second, out T third, out T fourth, out T fifth, out T[] rest)
        {
            first = array[0];
            second = array[1];
            third = array[2];
            fourth = array[3];
            fifth = array[4];
            rest = GetRestOfArray(array, 5);
        }
        public static void Deconstruct<T>(this T[] array, out T first, out T second, out T third, out T fourth, out T fifth, out T sixth, out T[] rest)
        {
            first = array[0];
            second = array[1];
            third = array[2];
            fourth = array[3];
            fifth = array[4];
            sixth = array[5];
            rest = GetRestOfArray(array, 6);
        }
        public static void Deconstruct<T>(this T[] array, out T first, out T second, out T third, out T fourth, out T fifth, out T sixth, out T seventh, out T[] rest)
        {
            first = array[0];
            second = array[1];
            third = array[2];
            fourth = array[3];
            fifth = array[4];
            sixth = array[5];
            seventh = array[6];
            rest = GetRestOfArray(array, 7);
        }
        public static void Deconstruct<T>(this T[] array, out T first, out T second, out T third, out T fourth, out T fifth, out T sixth, out T seventh, out T eighth, out T[] rest)
        {
            first = array[0];
            second = array[1];
            third = array[2];
            fourth = array[3];
            fifth = array[4];
            sixth = array[5];
            seventh = array[6];
            eighth = array[7];
            rest = GetRestOfArray(array, 8);
        }
        private static T[] GetRestOfArray<T>(T[] array, int skip)
        {
            return array.Skip(skip).ToArray();
        }
    }
}
