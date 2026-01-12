using MelonLoader;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using UnhollowerRuntimeLib;

namespace IEYTD2_SubmarineCode
{
    public static class AttachUnityScript
    {
        public static void AttachScriptToGameObject(string objectName, string scriptName)
        {
            GameObject target = GameObject.Find(objectName);
            if (target == null)
                return;

            Assembly assembly = Assembly.GetExecutingAssembly();
            Type scriptType = assembly
                .GetTypes()
                .FirstOrDefault(type => type.Name == scriptName);

            if (scriptType == null)
                return;

            var il2cppType = Il2CppType.From(scriptType);

            if (target.GetComponent(il2cppType) != null)
                return;

            target.AddComponent(il2cppType);
        }
    }
}
