using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerrariaWiringVisual
{
    internal class VisualizerWorld : ModSystem
    {
        private class WireSegment
        {
            public bool red;
            public bool blue;
            public bool green;
            public bool yellow;

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

        public static bool ShowWireSkip = false;
        public static bool ShowGatesDone = true;
        public static bool ShowUpcomingGates = true;
        public static bool ShowTriggeredLamps = false;
        public static bool ShowTeleporters = true;
        public static bool ShowPumps = true;

        private static readonly Color ColorWRed = new Color(255, 0, 0, 128);
        private static readonly Color ColorWBlue = new Color(0, 0, 255, 128);
        private static readonly Color ColorWGreen = new Color(0, 255, 0, 128);
        private static readonly Color ColorWYellow = new Color(255, 255, 0, 128);

        private static List<Rectangle> StartHighlight;
        private static Dictionary<Point16, WireSegment> WireHighlight;
        private static Point16 PointHighlight = Point16.Zero;
        private static Dictionary<Point16, ColoredMark> MarkCache;

        private static Texture2D pixel;

        private static Dictionary<Point16, bool> WiringGatesDone;
        private static Queue<Point16> WiringGatesCurrent;
        private static Queue<Point16> WiringGatesNext; //We need static references for both of these, because they get swapped around.
        private static Dictionary<Point16, bool> WiringWireSkip;
        private static Vector2[] WiringTeleporters;

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

                DrawIndicators();

                if (SuspendableWireManager.Running)
                {
                    DrawWireSegments();
                    DrawSimpleHeighlights();
                    DrawReflectionMarkers();
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
            }
        }

        private void DrawSimpleHeighlights()
        {
            Rectangle screenRect = GetScreenRect();

            foreach (var item in StartHighlight)
            {
                if (!screenRect.Contains(item.Location)) continue;

                DrawTileBorder(new Point16(item.Location), Color.Red, item.Width, item.Height);
            }
            if (SuspendableWireManager.Mode == SuspendableWireManager.SuspendMode.perSingle)
            {
                if (!screenRect.Contains(new Point(PointHighlight.X, PointHighlight.Y))) return;

                DrawTileBorder(PointHighlight, Color.Red);
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
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

            Rectangle screenRect = GetScreenRect();

            foreach (var item in WireHighlight)
            {
                if (!screenRect.Contains(new Point(item.Key.X, item.Key.Y))) continue;

                DrawWires(item.Key);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

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

        private void DrawTileMarker(Point16 tile, ColoredMark mark)
        {
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

        private Rectangle GetScreenRect(Vector2 offset = default)
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

        public static void ResetSegments()
        {
            WireHighlight.Clear();
            StartHighlight.Clear();
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
                case 1: segment.red = true; break;
                case 2: segment.blue = true; break;
                case 3: segment.green = true; break;
                case 4: segment.yellow = true; break;
            }
        }

        public static void AddStart(Rectangle trip)
        {
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

        protected void DrawWires(Point16 tileLoc)
        {
            Rectangle wireRect = new Rectangle(0, 0, 16, 16);
            Vector2 origin = Vector2.Zero;
            Vector2 positionOffset = Vector2.Zero;

            float scale = Main.GameViewMatrix.Zoom.X;

            int redWireVisibility = 0;
            int blueWireVisibility = 0;
            int greenWireVisibility = 0;
            int yellowWireVisibility = 0;

            bool hasLeftConnection = false;
            bool hasRightConnection = false;
            bool hasTopConnection = false;
            bool hasBottomConnection = false;
            float wireCount = 0f;

            int x = tileLoc.X;
            int y = tileLoc.Y;

            Tile tile = Main.tile[x, y];
            WireSegment wireCur;

            if (!WireHighlight.TryGetValue(new Point16(x, y), out wireCur)) return;

            bool hasWireTop, hasWireBottom, hasWireLeft, hasWireRight;
            WireSegment wireTop, wireBottom, wireLeft, wireRight;

            hasWireTop = WireHighlight.TryGetValue(new Point16(x, y - 1), out wireTop);
            hasWireBottom = WireHighlight.TryGetValue(new Point16(x, y + 1), out wireBottom);
            hasWireLeft = WireHighlight.TryGetValue(new Point16(x - 1, y), out wireLeft);
            hasWireRight = WireHighlight.TryGetValue(new Point16(x + 1, y), out wireRight);

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
                wireCount += 1f;
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
                Color redWireColor = Lighting.GetColor(x, y);
                switch (redWireVisibility)
                {
                    case 0:
                        redWireColor = Color.White;
                        break;
                    case 2:
                        redWireColor *= 0.5f;
                        break;
                    case 3:
                        redWireColor = Color.Transparent;
                        break;
                }
                if (redWireColor == Color.Transparent)
                {
                    wireCount -= 1f;
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
                wireCount += 1f;
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
                if (wireCount > 1f)
                {
                    wireOverlap = true;
                }
                wireRect.Y = textureYOffset + 18;
                wireRect.X = blueWireTextureX;
                Color blueWireColor = Lighting.GetColor(x, y);
                switch (blueWireVisibility)
                {
                    case 0:
                        blueWireColor = Color.White;
                        break;
                    case 2:
                        blueWireColor *= 0.5f;
                        break;
                    case 3:
                        blueWireColor = Color.Transparent;
                        break;
                }
                if (blueWireColor == Color.Transparent)
                {
                    wireCount -= 1f;
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
                wireCount += 1f;
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
                if (wireCount > 1f)
                {
                    wireOverlap = true;
                }
                wireRect.Y = textureYOffset + 36;
                wireRect.X = greenWireTextureX;
                Color greenWireColor = Lighting.GetColor(x, y);
                switch (greenWireVisibility)
                {
                    case 0:
                        greenWireColor = Color.White;
                        break;
                    case 2:
                        greenWireColor *= 0.5f;
                        break;
                    case 3:
                        greenWireColor = Color.Transparent;
                        break;
                }
                if (greenWireColor == Color.Transparent)
                {
                    wireCount -= 1f;
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
                wireCount += 1f;
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
                if (wireCount > 1f)
                {
                    wireOverlap = true;
                }
                wireRect.Y = textureYOffset + 54;
                wireRect.X = yellowWireTextureX;
                Color yellowWireColor = Lighting.GetColor(x, y);
                switch (yellowWireVisibility)
                {
                    case 0:
                        yellowWireColor = Color.White;
                        break;
                    case 2:
                        yellowWireColor *= 0.5f;
                        break;
                    case 3:
                        yellowWireColor = Color.Transparent;
                        break;
                }
                if (yellowWireColor == Color.Transparent)
                {
                    wireCount -= 1f;
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