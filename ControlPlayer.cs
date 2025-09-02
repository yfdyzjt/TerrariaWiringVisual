using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace TerrariaWiringVisualCopy
{
    internal class ControlPlayer : ModPlayer
    {
        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (Main.netMode == 0)
            {
                if (TerrariaWiringVisualCopy.keyStep.JustPressed)
                    SuspendableWireManager.Resume();

                if (TerrariaWiringVisualCopy.keyToggle.JustPressed)
                    SuspendableWireManager.Active = !SuspendableWireManager.Active;

                if (TerrariaWiringVisualCopy.keyAutoStep.JustPressed)
                    AutoStepWorld.Active = !AutoStepWorld.Active;

                if (TerrariaWiringVisualCopy.keySettings.JustPressed)
                    TerrariaWiringVisualCopy.settingsUI.Visible = !TerrariaWiringVisualCopy.settingsUI.Visible;
            }
        }
    }
}