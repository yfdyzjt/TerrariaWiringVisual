using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace TerrariaWiringVisual
{
    internal class ControlPlayer : ModPlayer
    {
        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (Main.netMode == 0)
            {
                if (TerrariaWiringVisual.keyStep.JustPressed)
                    SuspendableWireManager.Resume();

                if (TerrariaWiringVisual.keyToggle.JustPressed)
                    SuspendableWireManager.Active = !SuspendableWireManager.Active;

                if (TerrariaWiringVisual.keyAutoStep.JustPressed)
                    AutoStepWorld.Active = !AutoStepWorld.Active;

                if (TerrariaWiringVisual.keySettings.JustPressed)
                    TerrariaWiringVisual.settingsUI.Visible = !TerrariaWiringVisual.settingsUI.Visible;
            }
        }
    }
}