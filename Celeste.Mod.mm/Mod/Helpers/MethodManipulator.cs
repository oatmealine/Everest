using Mono.Cecil;
using System.Reflection;
using System.Collections.Generic;

namespace Celeste.Mod {
    static class MethodManipulator {
        public static readonly AssemblyDefinition CelesteAssemblyDefinition = AssemblyDefinition.ReadAssembly("Celeste.exe");
        
        public static MethodDefinition getCelesteMethodDefinition(MethodInfo method) {
            var module = method.Module;
            var moduleDefinition = CelesteAssemblyDefinition.Modules.Single((e) => e.FileName == module.FullyQualifiedName);
            
            var type = method.DeclaringType;
            var typeDefinition = moduleDefinition.Types.Single((e) => e.FullName == type.FullName);
            
            var method = typeDefinition.Methods.Where((e) => e.Name == method.Name).Where((e) => e.ReturnType.FullName == method.ReturnType.FullName)[0]; // TODO: Compare parameters as well (don't know how to do this easily)
        }
        
        public static List<MethodReference> methodCalls(MethodDefinition method) {
            var out = new List<MethodReference>();
            
            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.OpCode == OpCodes.Call)
                {
                    MethodReference methodCall = instruction.Operand as MethodReference;
                    out.Add(methodCall);
                }
            }
            
            return out;
        }
        
        public class MethodModifier {
            public MethodDefinition Method;
            
            public List<MethodReference> CalledMethods;
            
            public MethodModifier(MethodDefinition method) {
                Method = method;
                CalledMethods = MethodManipulator.methodCalls(method);
            }
            
            public class HookableMethod {
                public MethodReference InnerMethod;
                
                public List<dynamic> FakedParameters;
                
                public HookableMethod(MethodReference method) {
                    InnerMethod = method;
                }
                
                public Hook
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
                    name, TypeAttributes.Sealed | TypeAttributes.Public, typeof(MulticastDelegate));

                var constructor = typeBuilder.DefineConstructor(
                    MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public,
                    CallingConventions.Standard, new[] { typeof(object), typeof(IntPtr) });
                constructor.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

                var parameters = method.GetParameters();

                var invokeMethod = typeBuilder.DefineMethod(
                    "Invoke", MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Public,
                    method.ReturnType, parameters.Select(p => p.ParameterType).ToArray());
                invokeMethod.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

                for (int i = 0; i < parameters.Length; i++)
                {
                    var parameter = parameters[i];
                    invokeMethod.DefineParameter(i + 1, ParameterAttributes.None, parameter.Name);
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
}
