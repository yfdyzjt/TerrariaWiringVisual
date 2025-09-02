using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerrariaWiringVisualCopy
{
    public class LightHackGlobalWall : GlobalWall
    {
        public override void ModifyLight(int i, int j, int type, ref float r, ref float g, ref float b)
        {
            if (SuspendableWireManager.Active)
            {
                r = MathHelper.Clamp(r + (VisualizerWorld.TileLightRate / 100f), 0, 1);
                g = MathHelper.Clamp(g + (VisualizerWorld.TileLightRate / 100f), 0, 1);
                b = MathHelper.Clamp(b + (VisualizerWorld.TileLightRate / 100f), 0, 1);
            }
        }
    }

    internal class VisualizerWorld : ModSystem
    {
        public class WireSegment
        {
            public bool red;
            public bool blue;
            public bool green;
            public bool yellow;

            public float redLight;
            public float blueLight;
            public float greenLight;
            public float yellowLight;

            public int redIter;
            public int blueIter;
            public int greenIter;
            public int yellowIter;

            public int NumWires()
            {
                int num = 0;
                if (red) num++;
                if (blue) num++;
                if (green) num++;
                if (yellow) num++;
                return num;
            }
        }

        private struct ColoredMark
        {
            public string mark;
            public Color color;

            public ColoredMark(string mark, Color color)
            {
                this.mark = mark; this.color = color;
            }
        }

        private const int maxWireVisual = 5000;

        public static bool ShowWireSkip = true;
        public static bool ShowGatesDone = true;
        public static bool ShowUpcomingGates = true;
        public static bool ShowTriggeredLamps = true;
        public static bool ShowTeleporters = true;
        public static bool ShowPumps = true;

        public static int TailSpeedRate = 6;
        public static int TailSubRate = 90;
        public static int AllSubRate = 90;
        public static int TileLightRate = 0;

        private static readonly Color ColorWRed = new Color(255, 0, 0, 128);
        private static readonly Color ColorWBlue = new Color(0, 0, 255, 128);
        private static readonly Color ColorWGreen = new Color(0, 255, 0, 128);
        private static readonly Color ColorWYellow = new Color(255, 255, 0, 128);

        private static int redIterCount;
        private static int blueIterCount;
        private static int greenIterCount;
        private static int yellowIterCount;

        private static List<Rectangle> StartHighlight;
        public static Dictionary<Point16, WireSegment> WireHighlight;
        private static Point16 PointHighlight = Point16.Zero;
        private static Dictionary<Point16, ColoredMark> MarkCache;

        private static Texture2D pixel;

        private static Dictionary<Point16, bool> WiringGatesDone;
        private static Queue<Point16> WiringGatesCurrent;
        private static Queue<Point16> WiringGatesNext; //We need static references for both of these, because they get swapped around.
        private static Dictionary<Point16, bool> WiringWireSkip;
        private static Vector2[] WiringTeleporters;

        public static bool IsWireHighlightNull { get { return WireHighlight == null || WireHighlight.Count == 0; } }

        public override void OnWorldLoad()
        {
            StartHighlight = new List<Rectangle>();
            WireHighlight = new Dictionary<Point16, WireSegment>();
            MarkCache = new Dictionary<Point16, ColoredMark>();

            WiringGatesDone = (Dictionary<Point16, bool>)typeof(Wiring).GetField("_GatesDone", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            WiringGatesCurrent = (Queue<Point16>)typeof(Wiring).GetField("_GatesCurrent", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            WiringGatesNext = Wiring._GatesNext;
            WiringWireSkip = (Dictionary<Point16, bool>)typeof(Wiring).GetField("_wireSkip", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            WiringTeleporters = new Vector2[8];
        }

        public override void PostDrawTiles()
        {
            if (pixel == null)
            {
                pixel = new Texture2D(Main.graphics.GraphicsDevice, 1, 1);
                pixel.SetData(new Color[] { Color.White });
            }
            if (SuspendableWireManager.Active)
            {
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

                if (!Main.hideUI)
                {
                    DrawIndicators();
                }

                if (SuspendableWireManager.Running || !IsWireHighlightNull)
                {
                    DrawReflectionMarkers();
                    DrawWireSegments();
                    DrawSimpleHeighlights();
                }

                Main.spriteBatch.End();
            }
        }

        private void DrawReflectionMarkers()
        {
            Rectangle screenRect = GetScreenRect();

            foreach (var item in MarkCache)
            {
                if (!screenRect.Contains(new Point(item.Key.X, item.Key.Y))) continue;

                DrawTileMarker(item.Key, item.Value);
                LightTileMarker(item.Key, item.Value);
            }
        }

        private void DrawSimpleHeighlights()
        {
            Rectangle screenRect = GetScreenRect();

            foreach (var item in StartHighlight)
            {
                if (!screenRect.Contains(item.Location)) continue;

                DrawTileBorder(new Point16(item.Location), Color.Red, item.Width, item.Height);
                LightTileBorder(new Point16(item.Location), Color.White, item.Width, item.Height);
            }
            if (SuspendableWireManager.Mode == SuspendableWireManager.SuspendMode.perSingle)
            {
                if (!screenRect.Contains(new Point(PointHighlight.X, PointHighlight.Y))) return;

                DrawTileBorder(PointHighlight, Color.Red);
                LightTileBorder(PointHighlight, Color.White);
            }
        }

        private void DrawIndicators()
        {
            Color indicatorColor = Color.Yellow;

            if (SuspendableWireManager.Running)
                indicatorColor = Color.Red;

            Main.spriteBatch.Draw(pixel, new Rectangle(Main.mouseX + 20, Main.mouseY - 20, 10, 10), indicatorColor);

            if (AutoStepWorld.Active)
                Main.spriteBatch.Draw(pixel, new Rectangle(Main.mouseX + 30, Main.mouseY - 20, 10, 10), Color.Green);
        }

        private void DrawWireSegments()
        {
            Rectangle screenRect = GetScreenRect();

            foreach (var item in WireHighlight)
            {
                if (screenRect.Contains(new Point(item.Key.X, item.Key.Y)))
                {
                    DrawWires(item.Key, item.Value);
                    LightWires(item.Key, item.Value);
                }
            }

            /*
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive);

            Rectangle screenRect = GetScreenRect();

            foreach (var item in WireHighlight)
            {
                if (!screenRect.Contains(new Point(item.Key.X, item.Key.Y))) continue;

                DrawTileBorder(item.Key, Color.White);

                int startY = 2;
                int height = (int)Math.Ceiling(14f / item.Value.NumWires());

                if (item.Value.red)
                {
                    Main.spriteBatch.Draw(pixel, WorldRectToScreen(new Rectangle(item.Key.X * 16, item.Key.Y * 16 + startY, 16, height)), ColorWRed);
                    startY += height;
                }
                if (item.Value.blue)
                {
                    Main.spriteBatch.Draw(pixel, WorldRectToScreen(new Rectangle(item.Key.X * 16, item.Key.Y * 16 + startY, 16, height)), ColorWBlue);
                    startY += height;
                }
                if (item.Value.green)
                {
                    Main.spriteBatch.Draw(pixel, WorldRectToScreen(new Rectangle(item.Key.X * 16, item.Key.Y * 16 + startY, 16, height)), ColorWGreen);
                    startY += height;
                }
                if (item.Value.yellow)
                {
                    Main.spriteBatch.Draw(pixel, WorldRectToScreen(new Rectangle(item.Key.X * 16, item.Key.Y * 16 + startY, 16, height)), ColorWYellow);
                }
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            */
        }

        private void LightTileMarker(Point16 tile, ColoredMark mark)
        {
            if (mark.mark == "O" || mark.mark == "?")
            {
                Lighting.AddLight(tile.X, tile.Y, Color.White.R / 255f, Color.White.G / 255f, Color.White.B / 255f);
            }
        }

        private void DrawTileMarker(Point16 tile, ColoredMark mark)
        {
            if (mark.mark == "O" || mark.mark == "?")
            {
                DrawTileBorder(tile, Color.White);
                Main.spriteBatch.Draw(pixel, WorldRectToScreen(new Rectangle(tile.X * 16, tile.Y * 16, 16, 16)), Color.White * 0.5f);

                for (int y = tile.Y - 1; ; y--)
                {
                    if (Main.tile[tile.X, y].HasTile && Main.tile[tile.X, y].TileType == TileID.LogicGateLamp)
                    {
                        DrawTileBorder(new Point16(tile.X, y), Color.White);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            /*
            else if (mark.mark == "X")
            {
                Main.spriteBatch.Draw(TextureAssets.Tile[TileID.LogicGate].Value,
                    WorldVector2ToScreen(new Vector2(tile.X * 16, tile.Y * 16)),
                    new Rectangle?(new Rectangle(Main.tile[tile.X, tile.Y].TileFrameX, Main.tile[tile.X, tile.Y].TileFrameY, 16, 16)),
                    new Color(0.3f, 0.3f, 0.3f),
                    0f,
                    Vector2.Zero,
                    Main.GameViewMatrix.Zoom.X,
                    SpriteEffects.None,
                    0f);

                for (int y = tile.Y - 1; ; y--)
                {
                    if (Main.tile[tile.X, y].HasTile && Main.tile[tile.X, y].TileType == TileID.LogicGateLamp)
                    {
                        Main.spriteBatch.Draw(TextureAssets.Tile[TileID.LogicGateLamp].Value,
                            WorldVector2ToScreen(new Vector2(tile.X * 16, y * 16)),
                            new Rectangle?(new Rectangle(Main.tile[tile.X, y].TileFrameX, Main.tile[tile.X, y].TileFrameY, 16, 16)),
                            new Color(0.3f, 0.3f, 0.3f),
                            0f,
                            Vector2.Zero,
                            Main.GameViewMatrix.Zoom.X,
                            SpriteEffects.None,
                            0f);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            */
            /*
            Vector2 text = FontAssets.MouseText.Value.MeasureString(mark.mark);
            Vector2 loc = new Vector2(
                (tile.X * 16 - Main.screenPosition.X + 8) * Main.GameViewMatrix.Zoom.X + 0.5f * Main.screenWidth * (1 - Main.GameViewMatrix.Zoom.X),
                (tile.Y * 16 - Main.screenPosition.Y + 12) * Main.GameViewMatrix.Zoom.Y + 0.5f * Main.screenHeight * (1 - Main.GameViewMatrix.Zoom.Y)
            ) - text * Main.GameViewMatrix.Zoom.X / 2;

            if (Main.LocalPlayer.gravDir == -1)
                loc.Y = Main.screenHeight - loc.Y - 16;

            Main.spriteBatch.DrawString(
                FontAssets.MouseText.Value,
                mark.mark,
                loc,
                mark.color,
                0.0f,
                Vector2.Zero,
                Main.GameViewMatrix.Zoom,
                SpriteEffects.None,
                0.0f
            );
            */
        }

        private void LightTileBorder(Point16 tile, Color color, int width = 1, int height = 1)
        {
            for (int x = tile.X; x < tile.X + width; x++)
            {
                for (int y = tile.Y; y < tile.Y + height; y++)
                {
                    Lighting.AddLight(x, y, color.R / 255f, color.G / 255f, color.B / 255f);
                }
            }

        }

        private void DrawTileBorder(Point16 tile, Color color, int width = 1, int height = 1)
        {
            var borderX = (int)(2 * Main.GameViewMatrix.Zoom.X);
            var borderY = (int)(2 * Main.GameViewMatrix.Zoom.Y);

            Rectangle rect = WorldRectToScreen(new Rectangle(tile.X * 16, tile.Y * 16, width * 16, height * 16));

            Main.spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, borderY), null, color);
            Main.spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + rect.Height, rect.Width, borderY), null, color);
            Main.spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, borderX, rect.Height), null, color);
            Main.spriteBatch.Draw(pixel, new Rectangle(rect.X + rect.Width, rect.Y, borderX, rect.Height + borderX), null, color);
        }

        private static Rectangle GetScreenRect(Vector2 offset = default)
        {
            if (offset == default)
                offset = Vector2.Zero;

            int x1 = (int)((Main.screenPosition.X - offset.X) / 16f - 1f);
            int x2 = (int)((Main.screenPosition.X + (float)Main.screenWidth + offset.X) / 16f) + 2;
            int y1 = (int)((Main.screenPosition.Y - offset.Y) / 16f - 1f);
            int y2 = (int)((Main.screenPosition.Y + (float)Main.screenHeight + offset.Y) / 16f) + 5;

            if (x1 < 0)
                x1 = 0;
            if (x2 > Main.maxTilesX)
                x2 = Main.maxTilesX;
            if (y1 < 0)
                y1 = 0;
            if (y2 > Main.maxTilesY)
                y2 = Main.maxTilesY;

            Point screenOverdrawOffset = Main.GetScreenOverdrawOffset();
            int iterX1 = x1 + screenOverdrawOffset.X;
            int iterY1 = y1 + screenOverdrawOffset.Y;
            int iterX2 = x2 - screenOverdrawOffset.X;
            int iterY2 = y2 - screenOverdrawOffset.Y;

            return new Rectangle(iterX1, iterY1, iterX2 - iterX1, iterY2 - iterY1);
        }

        private Vector2 WorldVector2ToScreen(Vector2 vector2)
        {
            Vector2 newVector2 = new(
                (float)Math.Floor((vector2.X - Main.screenPosition.X) * Main.GameViewMatrix.Zoom.X + 0.5f * Main.screenWidth * (1 - Main.GameViewMatrix.Zoom.X)),
                (float)Math.Floor((vector2.Y - Main.screenPosition.Y) * Main.GameViewMatrix.Zoom.Y + 0.5f * Main.screenHeight * (1 - Main.GameViewMatrix.Zoom.Y)));

            if (Main.LocalPlayer.gravDir == -1)
                newVector2.Y = Main.screenHeight - newVector2.Y;

            return newVector2;
        }

        private Rectangle WorldRectToScreen(Rectangle rect)
        {
            Rectangle newRect = new(
                (int)Math.Floor((rect.X - Main.screenPosition.X) * Main.GameViewMatrix.Zoom.X + 0.5f * Main.screenWidth * (1 - Main.GameViewMatrix.Zoom.X)),
                (int)Math.Floor((rect.Y - Main.screenPosition.Y) * Main.GameViewMatrix.Zoom.Y + 0.5f * Main.screenHeight * (1 - Main.GameViewMatrix.Zoom.Y)),
                (int)Math.Floor(rect.Width * Main.GameViewMatrix.Zoom.X),
                (int)Math.Floor(rect.Height * Main.GameViewMatrix.Zoom.Y)
            );

            if (Main.LocalPlayer.gravDir == -1)
                newRect.Y = Main.screenHeight - newRect.Y - newRect.Height;

            return newRect;
        }

        public static void ResetStartSegments()
        {
            StartHighlight?.Clear();
        }

        public static void AddAllLogicGate(int centerX, int centerY)
        {
            var screen = GetScreenRect();
            var processedGates = new HashSet<Point16>();
            var foundComponentGates = new HashSet<Point16>();
            var componentsByMinRadius = new SortedDictionary<int, List<List<Point16>>>();
            int maxRadius = Math.Max(
                screen.Width / 2 + Math.Abs(centerX - screen.Center.X),
                screen.Height / 2 + Math.Abs(centerY - screen.Center.Y)
                ) + 8;
            for (int radius = 0; radius <= maxRadius; radius++)
            {
                var processedRadii = new List<int>();
                foreach (var kvp in componentsByMinRadius.Where(kvp => kvp.Key <= radius))
                {
                    var largerComponents = componentsByMinRadius
                            .Where(otherKvp => otherKvp.Key > radius)
                            .SelectMany(otherKvp => otherKvp.Value);
                    var processedComponents = new List<List<Point16>>();
                    foreach (var component in kvp.Value
                    .OrderByDescending(c => c.Count)
                    .ThenBy(c => c[0].X != c.Last().X)
                    .ThenBy(c => Math.Abs(c[0].X - centerX))
                    .ThenBy(c => Math.Abs(c[0].Y - centerY)))
                    {
                        var gatesInLargerComponents = largerComponents
                            .Where(c => c.Count > component.Count)
                            .SelectMany(c => c)
                            .ToHashSet();
                        if (component.Any(gatesInLargerComponents.Contains)) continue;

                        foreach (var gatePos in component)
                        {
                            if (processedGates.Add(gatePos))
                            {
                                AddGateToVisualizer(gatePos);
                            }
                        }
                        processedComponents.Add(component);
                    }
                    foreach(var r in processedComponents)
                    {
                        kvp.Value.Remove(r);
                    }
                    if (kvp.Value.Count == 0) processedRadii.Add(kvp.Key);
                }
                foreach (var r in processedRadii)
                {
                    componentsByMinRadius.Remove(r);
                }

                int minX = centerX - radius;
                int maxX = centerX + radius;
                int minY = centerY - radius;
                int maxY = centerY + radius;

                for (int x = minX; x <= maxX; x++)
                {
                    ProcessTileAtForDiscovery(x, minY);
                    if (minY != maxY) ProcessTileAtForDiscovery(x, maxY);
                }
                for (int y = minY; y <= maxY; y++)
                {
                    ProcessTileAtForDiscovery(minX, y);
                    if (minX != maxX) ProcessTileAtForDiscovery(maxX, y);
                }
            }

            void ProcessTileAtForDiscovery(int x, int y)
            {
                var position = new Point16(x, y);
                if (!IsPotentialStartGate(position)) return;

                var components = FindLogicGateComponent(position);

                foreach (var component in components)
                {
                    int componentMinRadius = 0;
                    foreach (var gatePos in component)
                    {
                        componentMinRadius = Math.Max(componentMinRadius,
                            Math.Max(
                                Math.Abs(gatePos.X - centerX),
                                Math.Abs(gatePos.Y - centerY)));
                    }
                    if (!componentsByMinRadius.TryGetValue(componentMinRadius, out var list))
                    {
                        list = [];
                        componentsByMinRadius[componentMinRadius] = list;
                    }
                    list.Add(component);
                    foundComponentGates.UnionWith(component);
                }
            }

            List<List<Point16>> FindLogicGateComponent(Point16 startPos)
            {
                var components = new List<List<Point16>>();
                var foundOffsets = new List<Point16>();

                int startGateType = GetLogicGateType(startPos.X, startPos.Y);
                if (startGateType == 0) return components;

                const int searchRadius = 8;

                var directions = new[] { new Point16(1, 0), new Point16(0, 1), new Point16(-1, 0), new Point16(0, -1) };
                for (int i = 1; i <= searchRadius; i++)
                {
                    foreach (var dir in directions)
                    {
                        var offsetPos = new Point16((short)(dir.X * i), (short)(dir.Y * i));
                        var offsetCheckPos = new Point16(startPos.X + offsetPos.X, startPos.Y + offsetPos.Y);

                        if (IsGateInSameComponent(offsetCheckPos, startGateType))
                        {
                            foundOffsets.Add(offsetPos);
                        }
                    }
                }
                if (foundOffsets.Count == 0) return components;

                int Gcd(int a, int b)
                {
                    a = Math.Abs(a);
                    b = Math.Abs(b);
                    while (b != 0)
                    {
                        int temp = b;
                        b = a % b;
                        a = temp;
                    }
                    return a;
                }

                Point16 GetCanonicalDirection(Point16 offset)
                {
                    if (offset.X == 0 && offset.Y == 0)
                        return Point16.Zero;
                    int commonDivisor = Gcd(offset.X, offset.Y);
                    var dx = (short)(offset.X / commonDivisor);
                    var dy = (short)(offset.Y / commonDivisor);
                    return new Point16(Math.Abs(dx), Math.Abs(dy));
                }

                var directionGroups = foundOffsets.GroupBy(GetCanonicalDirection);
                foreach (var group in directionGroups)
                {
                    var bestComponent = new List<Point16>();
                    foreach (var offset in group)
                    {
                        var component = new List<Point16> { startPos };
                        foreach (var newOffset in new List<Point16> { offset, new((short)-offset.X, (short)-offset.Y) })
                        {
                            for (int i = 1; ; i++)
                            {
                                var curPos = new Point16(startPos.X + newOffset.X * i, startPos.Y + newOffset.Y * i);
                                if (IsGateInSameComponent(curPos, startGateType))
                                {
                                    component.Add(curPos);
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                        if (component.Count > bestComponent.Count)
                        {
                            bestComponent = component;
                        }
                    }
                    if (bestComponent.Count > 1)
                    {
                        components.Add([.. bestComponent
                            .OrderBy(p => Math.Abs(p.X - centerX))
                            .ThenBy(p => Math.Abs(p.Y - centerY))]);
                    }
                }
                return components;
            }

            bool IsPotentialStartGate(Point16 pos)
            {
                return IsOnScreen(pos.X, pos.Y) && IsLogicGate(pos.X, pos.Y) && !foundComponentGates.Contains(pos);
            }

            bool IsGateInSameComponent(Point16 pos, int type)
            {
                return IsOnScreen(pos.X, pos.Y) && GetLogicGateType(pos.X, pos.Y) == type;
            }

            void AddGateToVisualizer(Point16 pos)
            {
                SuspendableWireManager.BeginTripWire(pos.X, pos.Y, 1, 1);
            }

            bool IsOnScreen(int x, int y)
            {
                return x >= screen.Left && x < screen.Right && y >= screen.Top && y < screen.Bottom;
            }

            bool IsLogicGate(int x, int y)
            {
                return GetLogicGateType(x, y) != 0;
            }

            int GetLogicGateType(int x, int y)
            {
                Tile tile = Main.tile[x, y];
                if (tile != null && tile.HasTile && tile.TileType == TileID.LogicGate)
                {
                    return tile.TileFrameX / 18 + 1;
                }
                return 0;
            }
        }

        public static void ResetWireSegments()
        {
            StartHighlight?.Clear();
            WireHighlight?.Clear();
        }

        public static void AddWireSegment(Point16 point, int color)
        {
            PointHighlight = point;

            WireSegment segment;
            if (!WireHighlight.TryGetValue(point, out segment))
            {
                /*
                if (WireHighlight.Count > maxWireVisual)
                    return;
                */

                segment = new WireSegment();
                WireHighlight.Add(point, segment);
            }

            switch (color)
            {
                case 1:
                    if (segment.red != true)
                    {
                        segment.red = true;
                        segment.redLight = 0f;
                        segment.redIter = redIterCount;
                    }
                    redIterCount++;
                    break;
                case 2:
                    if (segment.blue != true)
                    {
                        segment.blue = true;
                        segment.blueLight = 0f;
                        segment.blueIter = blueIterCount;
                    }
                    blueIterCount++;
                    break;
                case 3:
                    if (segment.green != true)
                    {
                        segment.green = true;
                        segment.greenLight = 0f;
                        segment.greenIter = greenIterCount;
                    }
                    greenIterCount++;
                    break;
                case 4:
                    if (segment.yellow != true)
                    {
                        segment.yellow = true;
                        segment.yellowLight = 0f;
                        segment.yellowIter = yellowIterCount;
                    }
                    yellowIterCount++;
                    break;
            }
        }

        public static void AddStart(Rectangle trip)
        {
            redIterCount = 0;
            blueIterCount = 0;
            greenIterCount = 0;
            yellowIterCount = 0;

            StartHighlight.Add(trip);
        }

        public static void ReportTeleporterArray(Vector2[] arr)
        {
            WiringTeleporters = arr;
        }

        public static void BuildMarkerCache()
        {
            MarkCache.Clear();

            if (ShowWireSkip)
            {
                foreach (var item in WiringWireSkip)
                {
                    if (item.Value)
                    {
                        MarkCache[item.Key] = new ColoredMark("X", Color.Red);
                    }
                }
            }

            if (ShowGatesDone)
            {
                foreach (var item in WiringGatesDone)
                {
                    if (item.Value)
                    {
                        MarkCache[item.Key] = new ColoredMark("X", Color.White);
                    }
                }
            }

            if (ShowUpcomingGates)
            {
                foreach (var item in WiringGatesCurrent)
                {
                    MarkCache[item] = new ColoredMark("O", Color.Red);
                }
                foreach (var item in WiringGatesNext)
                {
                    MarkCache[item] = new ColoredMark("O", Color.Red);
                }
            }

            if (ShowTriggeredLamps)
            {
                foreach (var item in Wiring._LampsToCheck)
                {
                    MarkCache[item] = new ColoredMark("?", Color.Orange);
                }
            }

            if (ShowTeleporters)
            {
                for (int i = 0; i < 8; i++)
                {
                    Vector2 v = WiringTeleporters[i];
                    if (v != null && v.X >= 0 && v.Y >= 0)
                    {
                        while (MarkCache.ContainsKey(v.ToPoint16()))
                            v.X++;

                        MarkCache[v.ToPoint16()] = new ColoredMark((i / 2 + 1).ToString(), Color.White);
                    }
                }
            }

            if (ShowPumps)
            {
                for (int i = 0; i < Wiring._numInPump; i++)
                {
                    Point16 point = new Point16(Wiring._inPumpX[i], Wiring._inPumpY[i]);
                    MarkCache[point] = new ColoredMark(i.ToString(), Color.Red);
                }
                for (int i = 0; i < Wiring._numOutPump; i++)
                {
                    Point16 point = new Point16(Wiring._outPumpX[i], Wiring._outPumpY[i]);
                    MarkCache[point] = new ColoredMark(i.ToString(), Color.Green);
                }
            }
        }

        public static void AllLightIter()
        {
            foreach (var item in WireHighlight)
            {
                WiresIter(item.Key, item.Value);
            }
        }

        private static void WiresIter(Point16 tileLoc, WireSegment wireCur)
        {
            var minLight = 1f;
            var maxLight = 1.3f;
            if (wireCur.red)
            {
                if (wireCur.redIter < 0)
                {
                    var newLight = maxLight + (maxLight - minLight) * wireCur.redIter / TailSubRate;
                    wireCur.redLight = Math.Max(minLight, Math.Min(maxLight, newLight));
                }

                wireCur.redIter -= TailSpeedRate;
            }
            if (wireCur.blue)
            {
                if (wireCur.blueIter < 0)
                {
                    var newLight = maxLight + (maxLight - minLight) * wireCur.blueIter / TailSubRate;
                    wireCur.blueLight = Math.Max(minLight, Math.Min(maxLight, newLight));
                }

                wireCur.blueIter -= TailSpeedRate;
            }
            if (wireCur.green)
            {
                if (wireCur.greenIter < 0)
                {
                    var newLight = maxLight + (maxLight - minLight) * wireCur.greenIter / TailSubRate;
                    wireCur.greenLight = Math.Max(minLight, Math.Min(maxLight, newLight));
                }

                wireCur.greenIter -= TailSpeedRate;
            }
            if (wireCur.yellow)
            {
                if (wireCur.yellowIter < 0)
                {
                    var newLight = maxLight + (maxLight - minLight) * wireCur.yellowIter / TailSubRate;
                    wireCur.yellowLight = Math.Max(minLight, Math.Min(maxLight, newLight));
                }

                wireCur.yellowIter -= TailSpeedRate;
            }
            if (!(wireCur.red || wireCur.blue || wireCur.green || wireCur.yellow))
            {
                WireHighlight.Remove(tileLoc);
            }
        }

        private void LightWires(Point16 tileLoc, WireSegment wireCur)
        {
            int x = tileLoc.X;
            int y = tileLoc.Y;

            // Lighting.GlobalBrightness = 1.5f;

            Vector3 lightColor;

            if (wireCur.red)
            {
                TorchID.TorchColor(TorchID.Red, out lightColor.X, out lightColor.Y, out lightColor.Z);
                lightColor *= wireCur.redLight;
                Lighting.AddLight(x, y, lightColor.X, lightColor.Y, lightColor.Z);
            }
            if (wireCur.blue)
            {
                TorchID.TorchColor(TorchID.Blue, out lightColor.X, out lightColor.Y, out lightColor.Z);
                lightColor *= wireCur.blueLight;
                Lighting.AddLight(x, y, lightColor.X, lightColor.Y, lightColor.Z);
            }
            if (wireCur.green)
            {
                TorchID.TorchColor(TorchID.Green, out lightColor.X, out lightColor.Y, out lightColor.Z);
                lightColor *= wireCur.greenLight;
                Lighting.AddLight(x, y, lightColor.X, lightColor.Y, lightColor.Z);
            }
            if (wireCur.yellow)
            {
                TorchID.TorchColor(TorchID.Yellow, out lightColor.X, out lightColor.Y, out lightColor.Z);
                lightColor *= wireCur.yellowLight;
                Lighting.AddLight(x, y, lightColor.X, lightColor.Y, lightColor.Z);
            }

        }

        private void DrawWires(Point16 tileLoc, WireSegment wireCur)
        {
            Rectangle wireRect = new Rectangle(0, 0, 16, 16);
            Vector2 origin = Vector2.Zero;
            Vector2 positionOffset = Vector2.Zero;

            float scale = Main.GameViewMatrix.Zoom.X;

            Color redWireColor = Color.White * wireCur.redLight;
            Color blueWireColor = Color.White * wireCur.blueLight;
            Color greenWireColor = Color.White * wireCur.greenLight;
            Color yellowWireColor = Color.White * wireCur.yellowLight;

            int wireCount = 0;

            int x = tileLoc.X;
            int y = tileLoc.Y;

            Tile tile = Main.tile[x, y];

            bool hasWireTop = WireHighlight.TryGetValue(new Point16(x, y - 1), out WireSegment wireTop);
            bool hasWireBottom = WireHighlight.TryGetValue(new Point16(x, y + 1), out WireSegment wireBottom);
            bool hasWireLeft = WireHighlight.TryGetValue(new Point16(x - 1, y), out WireSegment wireLeft);
            bool hasWireRight = WireHighlight.TryGetValue(new Point16(x + 1, y), out WireSegment wireRight);

            bool hasLeftConnection = false;
            bool hasRightConnection = false;
            bool hasTopConnection = false;
            bool hasBottomConnection = false;

            int textureYOffset = 0;
            if (tile.HasTile)
            {
                if (tile.TileType == 424)
                {
                    switch (tile.TileFrameX / 18)
                    {
                        case 0:
                            textureYOffset += 72;
                            break;
                        case 1:
                            textureYOffset += 144;
                            break;
                        case 2:
                            textureYOffset += 216;
                            break;
                    }
                }
                else if (tile.TileType == 445)
                {
                    textureYOffset += 72;
                }
            }
            if (wireCur.red)
            {
                wireCount += 1;
                int redWireTextureX = 0;
                if (hasWireTop && wireTop.red)
                {
                    redWireTextureX += 18;
                    hasTopConnection = true;
                }
                if (hasWireRight && wireRight.red)
                {
                    redWireTextureX += 36;
                    hasRightConnection = true;
                }
                if (hasWireBottom && wireBottom.red)
                {
                    redWireTextureX += 72;
                    hasBottomConnection = true;
                }
                if (hasWireLeft && wireLeft.red)
                {
                    redWireTextureX += 144;
                    hasLeftConnection = true;
                }
                wireRect.Y = textureYOffset;
                wireRect.X = redWireTextureX;
                if (redWireColor == Color.Transparent)
                {
                    wireCount -= 1;
                }
                else
                {
                    Main.spriteBatch.Draw(TextureAssets.WireNew.Value, WorldVector2ToScreen(new Vector2(x * 16, y * 16) + positionOffset), new Rectangle?(wireRect), redWireColor, 0f, origin, scale, SpriteEffects.None, 0f);
                }
            }
            if (wireCur.blue)
            {
                bool wireOverlap;
                bool blueBottomConnection;
                bool blueTopConnection;
                bool blueLeftConnection;
                bool blueRightConnection = blueLeftConnection = (blueTopConnection = (blueBottomConnection = (wireOverlap = false)));
                wireCount += 1;
                int blueWireTextureX = 0;
                if (hasWireTop && wireTop.blue)
                {
                    blueWireTextureX += 18;
                    blueTopConnection = true;
                    if (hasTopConnection)
                    {
                        wireOverlap = true;
                    }
                }
                if (hasWireRight && wireRight.blue)
                {
                    blueWireTextureX += 36;
                    blueRightConnection = true;
                    if (hasRightConnection)
                    {
                        wireOverlap = true;
                    }
                }
                if (hasWireBottom && wireBottom.blue)
                {
                    blueWireTextureX += 72;
                    blueBottomConnection = true;
                    if (hasBottomConnection)
                    {
                        wireOverlap = true;
                    }
                }
                if (hasWireLeft && wireLeft.blue)
                {
                    blueWireTextureX += 144;
                    blueLeftConnection = true;
                    if (hasLeftConnection)
                    {
                        wireOverlap = true;
                    }
                }
                if (wireCount > 1)
                {
                    wireOverlap = true;
                }
                wireRect.Y = textureYOffset + 18;
                wireRect.X = blueWireTextureX;
                if (blueWireColor == Color.Transparent)
                {
                    wireCount -= 1;
                }
                else
                {
                    Main.spriteBatch.Draw(TextureAssets.WireNew.Value, WorldVector2ToScreen(new Vector2(x * 16, y * 16) + positionOffset), new Rectangle?(wireRect), blueWireColor * (1f / wireCount), 0f, origin, scale, SpriteEffects.None, 0f);
                    if (blueTopConnection)
                    {
                        if (wireOverlap && !hasTopConnection)
                        {
                            Main.spriteBatch.Draw(TextureAssets.WireNew.Value, WorldVector2ToScreen(new Vector2(x * 16, y * 16) + positionOffset), new Rectangle?(new Rectangle(18, wireRect.Y, 16, 6)), blueWireColor, 0f, origin, scale, SpriteEffects.None, 0f);
                        }
                        hasTopConnection = true;
                    }
                    if (blueBottomConnection)
                    {
                        if (wireOverlap && !hasBottomConnection)
                        {
                            Main.spriteBatch.Draw(TextureAssets.WireNew.Value, WorldVector2ToScreen(new Vector2(x * 16, y * 16) + positionOffset + new Vector2(0f, 10f)), new Rectangle?(new Rectangle(72, wireRect.Y + 10, 16, 6)), blueWireColor, 0f, origin, scale, SpriteEffects.None, 0f);
                        }
                        hasBottomConnection = true;
                    }
                    if (blueRightConnection)
                    {
                        if (wireOverlap && !hasRightConnection)
                        {
                            Main.spriteBatch.Draw(TextureAssets.WireNew.Value, WorldVector2ToScreen(new Vector2(x * 16, y * 16) + positionOffset + new Vector2(10f, 0f)), new Rectangle?(new Rectangle(46, wireRect.Y, 6, 16)), blueWireColor, 0f, origin, scale, SpriteEffects.None, 0f);
                        }
                        hasRightConnection = true;
                    }
                    if (blueLeftConnection)
                    {
                        if (wireOverlap && !hasLeftConnection)
                        {
                            Main.spriteBatch.Draw(TextureAssets.WireNew.Value, WorldVector2ToScreen(new Vector2(x * 16, y * 16) + positionOffset), new Rectangle?(new Rectangle(144, wireRect.Y, 6, 16)), blueWireColor, 0f, origin, scale, SpriteEffects.None, 0f);
                        }
                        hasLeftConnection = true;
                    }
                }
            }
            if (wireCur.green)
            {
                bool wireOverlap;
                bool greenBottomConnection;
                bool greenTopConnection;
                bool greenLeftConnection;
                bool greenRightConnection = greenLeftConnection = (greenTopConnection = (greenBottomConnection = (wireOverlap = false)));
                wireCount += 1;
                int greenWireTextureX = 0;
                if (hasWireTop && wireTop.green)
                {
                    greenWireTextureX += 18;
                    greenTopConnection = true;
                    if (hasTopConnection)
                    {
                        wireOverlap = true;
                    }
                }
                if (hasWireRight && wireRight.green)
                {
                    greenWireTextureX += 36;
                    greenRightConnection = true;
                    if (hasRightConnection)
                    {
                        wireOverlap = true;
                    }
                }
                if (hasWireBottom && wireBottom.green)
                {
                    greenWireTextureX += 72;
                    greenBottomConnection = true;
                    if (hasBottomConnection)
                    {
                        wireOverlap = true;
                    }
                }
                if (hasWireLeft && wireLeft.green)
                {
                    greenWireTextureX += 144;
                    greenLeftConnection = true;
                    if (hasLeftConnection)
                    {
                        wireOverlap = true;
                    }
                }
                if (wireCount > 1)
                {
                    wireOverlap = true;
                }
                wireRect.Y = textureYOffset + 36;
                wireRect.X = greenWireTextureX;
                if (greenWireColor == Color.Transparent)
                {
                    wireCount -= 1;
                }
                else
                {
                    Main.spriteBatch.Draw(TextureAssets.WireNew.Value, WorldVector2ToScreen(new Vector2(x * 16, y * 16) + positionOffset), new Rectangle?(wireRect), greenWireColor * (1f / wireCount), 0f, origin, scale, SpriteEffects.None, 0f);
                    if (greenTopConnection)
                    {
                        if (wireOverlap && !hasTopConnection)
                        {
                            Main.spriteBatch.Draw(TextureAssets.WireNew.Value, WorldVector2ToScreen(new Vector2(x * 16, y * 16) + positionOffset), new Rectangle?(new Rectangle(18, wireRect.Y, 16, 6)), greenWireColor, 0f, origin, scale, SpriteEffects.None, 0f);
                        }
                        hasTopConnection = true;
                    }
                    if (greenBottomConnection)
                    {
                        if (wireOverlap && !hasBottomConnection)
                        {
                            Main.spriteBatch.Draw(TextureAssets.WireNew.Value, WorldVector2ToScreen(new Vector2(x * 16, y * 16) + positionOffset + new Vector2(0f, 10f)), new Rectangle?(new Rectangle(72, wireRect.Y + 10, 16, 6)), greenWireColor, 0f, origin, scale, SpriteEffects.None, 0f);
                        }
                        hasBottomConnection = true;
                    }
                    if (greenRightConnection)
                    {
                        if (wireOverlap && !hasRightConnection)
                        {
                            Main.spriteBatch.Draw(TextureAssets.WireNew.Value, WorldVector2ToScreen(new Vector2(x * 16, y * 16) + positionOffset + new Vector2(10f, 0f)), new Rectangle?(new Rectangle(46, wireRect.Y, 6, 16)), greenWireColor, 0f, origin, scale, SpriteEffects.None, 0f);
                        }
                        hasRightConnection = true;
                    }
                    if (greenLeftConnection)
                    {
                        if (wireOverlap && !hasLeftConnection)
                        {
                            Main.spriteBatch.Draw(TextureAssets.WireNew.Value, WorldVector2ToScreen(new Vector2(x * 16, y * 16) + positionOffset), new Rectangle?(new Rectangle(144, wireRect.Y, 6, 16)), greenWireColor, 0f, origin, scale, SpriteEffects.None, 0f);
                        }
                        hasLeftConnection = true;
                    }
                }
            }
            if (wireCur.yellow)
            {
                bool wireOverlap;
                bool yellowBottomConnection;
                bool yellowTopConnection;
                bool yellowLeftConnection;
                bool yellowRightConnection = yellowLeftConnection = (yellowTopConnection = (yellowBottomConnection = (wireOverlap = false)));
                wireCount += 1;
                int yellowWireTextureX = 0;
                if (hasWireTop && wireTop.yellow)
                {
                    yellowWireTextureX += 18;
                    yellowTopConnection = true;
                    if (hasTopConnection)
                    {
                        wireOverlap = true;
                    }
                }
                if (hasWireRight && wireRight.yellow)
                {
                    yellowWireTextureX += 36;
                    yellowRightConnection = true;
                    if (hasRightConnection)
                    {
                        wireOverlap = true;
                    }
                }
                if (hasWireBottom && wireBottom.yellow)
                {
                    yellowWireTextureX += 72;
                    yellowBottomConnection = true;
                    if (hasBottomConnection)
                    {
                        wireOverlap = true;
                    }
                }
                if (hasWireLeft && wireLeft.yellow)
                {
                    yellowWireTextureX += 144;
                    yellowLeftConnection = true;
                    if (hasLeftConnection)
                    {
                        wireOverlap = true;
                    }
                }
                if (wireCount > 1)
                {
                    wireOverlap = true;
                }
                wireRect.Y = textureYOffset + 54;
                wireRect.X = yellowWireTextureX;
                if (yellowWireColor == Color.Transparent)
                {
                    wireCount -= 1;
                }
                else
                {
                    Main.spriteBatch.Draw(TextureAssets.WireNew.Value, WorldVector2ToScreen(new Vector2(x * 16, y * 16) + positionOffset), new Rectangle?(wireRect), yellowWireColor * (1f / wireCount), 0f, origin, scale, SpriteEffects.None, 0f);
                    if (yellowTopConnection)
                    {
                        if (wireOverlap && !hasTopConnection)
                        {
                            Main.spriteBatch.Draw(TextureAssets.WireNew.Value, WorldVector2ToScreen(new Vector2(x * 16, y * 16) + positionOffset), new Rectangle?(new Rectangle(18, wireRect.Y, 16, 6)), yellowWireColor, 0f, origin, scale, SpriteEffects.None, 0f);
                        }
                    }
                    if (yellowBottomConnection)
                    {
                        if (wireOverlap && !hasBottomConnection)
                        {
                            Main.spriteBatch.Draw(TextureAssets.WireNew.Value, WorldVector2ToScreen(new Vector2(x * 16, y * 16) + positionOffset + new Vector2(0f, 10f)), new Rectangle?(new Rectangle(72, wireRect.Y + 10, 16, 6)), yellowWireColor, 0f, origin, scale, SpriteEffects.None, 0f);
                        }
                    }
                    if (yellowRightConnection)
                    {
                        if (wireOverlap && !hasRightConnection)
                        {
                            Main.spriteBatch.Draw(TextureAssets.WireNew.Value, WorldVector2ToScreen(new Vector2(x * 16, y * 16) + positionOffset + new Vector2(10f, 0f)), new Rectangle?(new Rectangle(46, wireRect.Y, 6, 16)), yellowWireColor, 0f, origin, scale, SpriteEffects.None, 0f);
                        }
                    }
                    if (yellowLeftConnection)
                    {
                        if (wireOverlap && !hasLeftConnection)
                        {
                            Main.spriteBatch.Draw(TextureAssets.WireNew.Value, WorldVector2ToScreen(new Vector2(x * 16, y * 16) + positionOffset), new Rectangle?(new Rectangle(144, wireRect.Y, 6, 16)), yellowWireColor, 0f, origin, scale, SpriteEffects.None, 0f);
                        }
                    }
                }
            }
        }

        override public void OnWorldUnload()
        {
            pixel = null;

            StartHighlight = null;
            WireHighlight = null;
            MarkCache = null;

            WiringGatesDone = null;
            WiringGatesCurrent = null;
            WiringGatesNext = null;
            WiringWireSkip = null;
            WiringTeleporters = null;
        }
    }
}