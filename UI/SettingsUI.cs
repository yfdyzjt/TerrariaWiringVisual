using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace TerrariaWiringVisualCopy.UI
{
    public class SettingsUI : UIState
    {
        public bool Visible = false;
        private UIPanel BasePanel;

        public override void OnInitialize()
        {
            HAlign = 0.25f;
            VAlign = 0.5f;
            Width = new StyleDimension(0, 0);
            Height = new StyleDimension(0, 0);

            BasePanel = new UIPanel();
            BasePanel.MarginLeft = 10;
            BasePanel.MarginRight = 10;
            BasePanel.MarginTop = 10;
            BasePanel.MarginBottom = 10;
            BasePanel.Width = new StyleDimension(0, 1);
            BasePanel.Height = new StyleDimension(0, 1);
            Append(BasePanel);

            UIIntBox arateBox = new UIIntBox(() => AutoStepWorld.Rate, x => AutoStepWorld.Rate = x, 3);
            arateBox.Width = new StyleDimension(0, 0.25f);
            UIIntBox trateBox = new UIIntBox(() => VisualizerWorld.TileLightRate, x => VisualizerWorld.TileLightRate = x, 3);
            trateBox.Width = new StyleDimension(0, 0.25f);
            UIIntBox asrateBox = new UIIntBox(() => VisualizerWorld.AllSubRate, x => VisualizerWorld.AllSubRate = x, 3);
            asrateBox.Width = new StyleDimension(0, 0.25f);
            UIIntBox tsrateBox = new UIIntBox(() => VisualizerWorld.TailSubRate, x => VisualizerWorld.TailSubRate = x, 3);
            tsrateBox.Width = new StyleDimension(0, 0.25f);
            UIIntBox srateBox = new UIIntBox(() => VisualizerWorld.TailSpeedRate, x => VisualizerWorld.TailSpeedRate = x, 3);
            srateBox.Width = new StyleDimension(0, 0.25f);

            UIElement[] elements = new UIElement[]
            {
                null,
                new UIText("TerrariaWiringVisualCopy Settings"),
                null,
                new UIText("Set mode:"),
                new UIToggle("Single", () => SuspendableWireManager.Mode == SuspendableWireManager.SuspendMode.perSingle, () => SuspendableWireManager.Mode = SuspendableWireManager.SuspendMode.perSingle),
                new UIToggle("Wire", () => SuspendableWireManager.Mode == SuspendableWireManager.SuspendMode.perWire, () => SuspendableWireManager.Mode = SuspendableWireManager.SuspendMode.perWire),
                new UIToggle("Source", () => SuspendableWireManager.Mode == SuspendableWireManager.SuspendMode.perSource, () => SuspendableWireManager.Mode = SuspendableWireManager.SuspendMode.perSource),
                new UIToggle("Stage", () => SuspendableWireManager.Mode == SuspendableWireManager.SuspendMode.perStage, () => SuspendableWireManager.Mode = SuspendableWireManager.SuspendMode.perStage),
                null,
                new UIText("Toggle visuals:"),
                new UIToggle("Wire skip", () => VisualizerWorld.ShowWireSkip, () => VisualizerWorld.ShowWireSkip = !VisualizerWorld.ShowWireSkip),
                new UIToggle("Gates done", () => VisualizerWorld.ShowGatesDone, () => VisualizerWorld.ShowGatesDone = !VisualizerWorld.ShowGatesDone),
                new UIToggle("Upcoming gates", () => VisualizerWorld.ShowUpcomingGates, () => VisualizerWorld.ShowUpcomingGates = !VisualizerWorld.ShowUpcomingGates),
                new UIToggle("Triggered lamps", () => VisualizerWorld.ShowTriggeredLamps, () => VisualizerWorld.ShowTriggeredLamps = !VisualizerWorld.ShowTriggeredLamps),
                new UIToggle("Triggered teleporters", () => VisualizerWorld.ShowTeleporters, () => VisualizerWorld.ShowTeleporters = !VisualizerWorld.ShowTeleporters),
                new UIToggle("Triggered pumps", () => VisualizerWorld.ShowPumps, () => VisualizerWorld.ShowPumps = !VisualizerWorld.ShowPumps),
                null,
                new UIText("Auto-step rate:"),
                arateBox,
                null,
                new UIText("Tile light rate:"),
                trateBox,
                null,
                new UIText("Wires sub rate:"),
                asrateBox,
                null,
                new UIText("Wires tail sub rate:"),
                tsrateBox,
                null,
                new UIText("Wires tail speed rate:"),
                srateBox,
                null,
                new UIAutoText(() => string.Format("Queued wire trips: {0}", SuspendableWireManager.QueuedNum)),
            };

            bool title = false;
            foreach (var item in elements)
            {
                if (item == null)
                {
                    title = true;
                    continue;
                }
                if (title)
                {
                    title = false;
                    AppendToPanel(item, 0);
                }
                else
                {
                    AppendToPanel(item, 20);
                }
            }

            Height.Pixels += 20;

            Recalculate();
        }

        private void AppendToPanel(UIElement element, float indent)
        {
            BasePanel.Append(element);
            element.Recalculate();
            element.Left = new StyleDimension(indent, 0);
            element.Top = new StyleDimension(Height.Pixels, 0);
            Height.Pixels += element.MinHeight.Pixels + 5;
            float newWidth = element.MinWidth.Pixels + indent + 20;
            if (Width.Pixels < newWidth)
                Width.Pixels = newWidth;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            if (BasePanel.IsMouseHovering)
                Main.LocalPlayer.mouseInterface = true;
            base.DrawSelf(spriteBatch);
        }
    }
}