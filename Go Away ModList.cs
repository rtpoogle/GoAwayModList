using BepInEx;
using HarmonyLib;
using BepInEx.Logging;
using LC_API.GameInterfaceAPI.Features;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;

namespace FacilityMeltdownPatch
{
    [BepInPlugin(GUID, NAME, VER)]
    [BepInDependency(LC_API.MyPluginInfo.PLUGIN_GUID)]
    public class GOMLPlugin : BaseUnityPlugin
    {
        public const string GUID = "xyz.poogle.goml";
        public const string NAME = "Go Away ModList";
        public const string VER = "1.0.1";
        public readonly Harmony harmony = new Harmony(GUID);

        public ManualLogSource LogSrc;

        public static GOMLPlugin Instance;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            LogSrc = BepInEx.Logging.Logger.CreateLogSource(GUID);

            Patches.InitPatches();

            LogSrc.LogInfo("Initialized!");

        }
    }

    internal class Patches
    {
        internal static void InitPatches()
        {
            GOMLPlugin.Instance.harmony.PatchAll();
        }

        [HarmonyPatch]
        public static class CheatDatabase_OtherPlayerCheatDetector_Patch
        {
            static readonly MethodInfo QueueTip = AccessTools.Method(typeof(Player), nameof(Player.QueueTip));
            static readonly MethodInfo LocalPlayer = AccessTools.PropertyGetter(typeof(Player), nameof(Player.LocalPlayer));
            static readonly MethodInfo Nop_With_Params = CreateDynamicNop();
            static readonly int LookAhead = QueueTip.GetParameterTypes().Length + 1; // + 1 since we are 1 instruction before arguments

            private static MethodInfo CreateDynamicNop()
            {
                DynamicMethod nop = new DynamicMethod("Nop", null, QueueTip.GetParameterTypes(), typeof(Player));
                nop.GetILGenerator().Emit(OpCodes.Ret);
                return nop;
            }

            public static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredMethods(typeof(LC_API.CheatDatabase)).Cast<MethodBase>();

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
            {
                GOMLPlugin.Instance.LogSrc.LogInfo("Patching " + original.Name);

                var code = new List<CodeInstruction>(instructions);

                for (int i = 0; i < code.Count - LookAhead; i++)
                {
                    if (code[i].Calls(LocalPlayer) && code[i + LookAhead].Calls(QueueTip))
                    {
                        code[i]             = new CodeInstruction(OpCodes.Nop); // nop the class load
                        code[i + LookAhead] = new CodeInstruction(OpCodes.Callvirt, Nop_With_Params); // replace QueueTip with dynamic nop
                    }
                }

                GOMLPlugin.Instance.LogSrc.LogInfo("Patched " + original.Name);

                return code;
            }
        }
    }
}
