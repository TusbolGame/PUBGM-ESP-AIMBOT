using System;
using System.Text;
using GameOverlay.Drawing;
using GameOverlay.Windows;
using SharpDX;
using Point = GameOverlay.Drawing.Point;
using Color = GameOverlay.Drawing.Color;
using Rectangle = GameOverlay.Drawing.Rectangle;
using RawVector2 = SharpDX.Mathematics.Interop.RawVector2;
using ShpVector3 = SharpDX.Vector3;
using ShpVector2 = SharpDX.Vector2;
using System.Windows.Forms;

namespace PUBGMESP
{
    public interface IAimbotForm
    {
        void Initialize();
        void Update();
    }
    public class AimTarget
    {
        public ShpVector2 Screen2D;
        public float CrosshairDistance;
        public int uniqueID;
    }
    public class AimbotForm : IAimbotForm
    {
        public readonly OverlayWindow _window;
        private readonly Graphics _graphics;
        private readonly GameMemSearch _ueSearch;

        private Font _font;
        private Font _infoFont;
        private Font _bigfont;
        private SolidBrush _black;
        private SolidBrush _red;
        private SolidBrush _green;
        private SolidBrush _blue;
        private SolidBrush _orange;
        private SolidBrush _purple;
        private SolidBrush _yellow;
        private SolidBrush _white;
        private SolidBrush _transparent;
        private SolidBrush _txtBrush;
        private SolidBrush[] _randomBrush;
        private SolidBrush _boxBrush;
        private  int BestTargetUniqID = -1;

        private DisplayData _data;
        private int playerCount;

        // offset
        private int actorOffset, boneOffset, tmpOffset;


        public AimbotForm(RECT rect, GameMemSearch ueSearch)
        {
            this._ueSearch = ueSearch;

            _window = new OverlayWindow(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top)
            {
                IsTopmost = true,
                IsVisible = true
            };

            _window.SizeChanged += _window_SizeChanged;

            _graphics = new Graphics
            {
                MeasureFPS = true,
                Height = _window.Height,
                PerPrimitiveAntiAliasing = true,
                TextAntiAliasing = true,
                UseMultiThreadedFactories = false,
                VSync = true,
                Width = _window.Width,
                WindowHandle = IntPtr.Zero
            };

            // offset
            actorOffset = 320;
            boneOffset = 1408;
            tmpOffset = 776;
        }

        ~AimbotForm()
        {
            _graphics.Dispose();
            _window.Dispose();
        }

        public void Initialize()
        {
            _window.CreateWindow();

            _graphics.WindowHandle = _window.Handle;
            _graphics.Setup();

            _font = _graphics.CreateFont("Microsoft YaHei", 10);
            _infoFont = _graphics.CreateFont("Microsoft YaHei", 12);
            _bigfont = _graphics.CreateFont("Microsoft YaHei", 18, true);

            _black = _graphics.CreateSolidBrush(0, 0, 0);
            _red = _graphics.CreateSolidBrush(255, 99, 71);
            _green = _graphics.CreateSolidBrush(Color.Green);
            _blue = _graphics.CreateSolidBrush(135, 206, 250);
            _orange = _graphics.CreateSolidBrush(255, 97, 0);
            _purple = _graphics.CreateSolidBrush(255, 105, 180);
            _yellow = _graphics.CreateSolidBrush(255, 255, 0);
            _white = _graphics.CreateSolidBrush(255, 255, 255);
            _transparent = _graphics.CreateSolidBrush(0, 0, 0, 0);
            _randomBrush = new SolidBrush[]
            {
                _orange,_red,_green,_blue,_yellow,_white,_purple
            };
            _txtBrush = _graphics.CreateSolidBrush(0, 0, 0, 0.5f);
        }

        public void UpdateData(DisplayData data)
        {
            _data = data;
        }

        public void Update()
        {
            var gfx = _graphics;
            gfx.BeginScene();
            gfx.ClearScene(_transparent);

            if (Settings.ShowMenu)
            {

                DrawShadowText(gfx, _font, _green, new Point(20f, _window.Height / 2 + 115), "Aimbot Menu");
                if (Settings.aimEnabled)
                {
                    DrawShadowText(gfx, _font, _red, new Point(20f, _window.Height / 2 + 130), "Aimbot ON    (F5) :  " + Settings.aimEnabled.ToString());
                }
                else
                {
                    DrawShadowText(gfx, _font, _green, new Point(20f, _window.Height / 2 + 130), "Aimbot OF    (F5) :  " + Settings.aimEnabled.ToString());
                }
                if (Settings.bDrawFow)
                {
                    DrawShadowText(gfx, _font, _red, new Point(20f, _window.Height / 2 + 145), "FOV SHOW    (F6) :  " + Settings.bDrawFow.ToString());
                }
                else
                {
                    DrawShadowText(gfx, _font, _green, new Point(20f, _window.Height / 2 + 145), "FOV HIDE    (F6) :  " + Settings.bDrawFow.ToString());
                }

                DrawShadowText(gfx, _font, _green, new Point(20f, _window.Height / 2 + 160), "┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈");
            }

            // Read View Matrix
            long viewMatrixAddr = Mem.ReadMemory<int>(Mem.ReadMemory<int>(_data.ViewMatrixBase) + 32) + 512;
            D3DMatrix viewMatrix = Algorithms.ReadViewMatrix(viewMatrixAddr);

            var AimTargets = new AimTarget[_data.Players.Length];
            float fClosestDist = -1;
            // Draw Player ESP
            if (Settings.PlayerESP)
            {
                for (int i = 0; i < _data.Players.Length; i++)
                {

                    var player = _data.Players[i];
                    //if (player.Health <= 0) continue;
                    if (Algorithms.WorldToScreenPlayer(viewMatrix, player.Position, out ShpVector3 playerScreen, out int distance, _window.Width, _window.Height))
                    {
                        // Too Far not render
                        if (distance > 500) continue;
                        float x = playerScreen.X;
                        float y = playerScreen.Y;
                        float h = playerScreen.Z;
                        float w = playerScreen.Z / 2;

                        try
                        {
                            _boxBrush = _randomBrush[player.TeamID % 7];
                        }
                        catch (IndexOutOfRangeException)
                        {
                            _boxBrush = _green;
                        }
                        //DrawShadowText(gfx,_font, _green, new Point((x - playerScreen.Z / 4) - 3, y - 15), player.Pose.ToString());

                        // Adjust Box
                        if (player.Pose == 1114636288)
                        {
                            y = playerScreen.Y + playerScreen.Z / 5;
                            h -= playerScreen.Z / 5;
                        }
                        if (player.Pose == 1112014848 || player.Status == 7)
                        {
                            y = playerScreen.Y + playerScreen.Z / 4;
                            h -= playerScreen.Z / 4;
                        }

                        int ScreenCenterX = _window.Width / 2, ScreenCenterY = _window.Height / 2;

                        if (Settings.aimEnabled)
                        {
                            long tmpAddv = Mem.ReadMemory<int>(player.Address + tmpOffset);
                            long bodyAddv = tmpAddv + actorOffset;
                            long boneAddv = Mem.ReadMemory<int>(tmpAddv + boneOffset) + 48;
                            ShpVector3 headPos = Algorithms.GetBoneWorldPosition(bodyAddv, boneAddv + 5 * 48);

                            headPos.Z += 7;

                            var clampPos = headPos - player.Position;
                            bool w2sHead = Algorithms.WorldToScreen3DBox(viewMatrix, new ShpVector3(headPos.X, headPos.Y - (Settings.bPredict * 2), headPos.Z - (Settings.bYAxis * 8)), out ShpVector2 HeadPosition, _window.Width, _window.Height);

                            AimTargets[i] = new AimTarget();
                            AimTargets[i].Screen2D = HeadPosition;
                            AimTargets[i].uniqueID = player.TeamID;
                            AimTargets[i].CrosshairDistance = ShpVector2.Distance(HeadPosition, new ShpVector2(ScreenCenterX, ScreenCenterY));

                            if (BestTargetUniqID == -1)
                            {
                                if (Algorithms.isInside(ScreenCenterX, ScreenCenterY, Settings.bFovArray[Settings.bFovInt], AimTargets[i].Screen2D.X, AimTargets[i].Screen2D.Y))
                                {
                                    fClosestDist = AimTargets[i].CrosshairDistance;
                                    BestTargetUniqID = AimTargets[i].uniqueID;
                                }
                            }
                            if (MainForm.GetAsyncKeyState(Settings.bAimKeys[Settings.bAimKeyINT]))
                            {
                                if (BestTargetUniqID != -1)
                                {
                                    var best = FindAimTargetByUniqueID(AimTargets, BestTargetUniqID);

                                    if (best != null)
                                    {
                                        {
                                            var roundPos = new ShpVector2((float)Math.Round(best.Screen2D.X), (float)Math.Round(best.Screen2D.Y));
                                            AimAtPosV2(roundPos.X, roundPos.Y, _window.Width, _window.Height, false);

                                        }
                                    }
                                }
                            }
                            else
                            {
                                BestTargetUniqID = -1;
                            }
                        }
                        if (Settings.bDrawFow)
                        {
                            gfx.DrawCircle(_red, ScreenCenterX, ScreenCenterY, Settings.bFovArray[Settings.bFovInt], 2);
                        }
                    }
                }

                gfx.EndScene();
            }
        }

        private static AimTarget FindAimTargetByUniqueID(AimTarget[] array, int uniqueID)
        {
            var entityList = array;
            for (int i = 0; i < entityList.Length; i++)
            {
                var current = entityList[i];
                if (current == null)
                    continue;

                if (current.uniqueID == uniqueID)
                    return current;
            }
            return null;
        }
        //uc port
        private  void AimAtPosV2(float x, float y, int Width, int Height , bool smooth)
        {
            int ScreenCenterX = Width / 2, ScreenCenterY = Height / 2;

            float AimSpeed = (float)Settings.bSmooth + 1f;;
            float TargetX = 0;
            float TargetY = 0;

            //X Axis
            if (x != 0)
            {
                if (x > ScreenCenterX)
                {
                    TargetX = -(ScreenCenterX - x);
                    TargetX /= AimSpeed;
                    if (TargetX + ScreenCenterX > ScreenCenterX * 2) TargetX = 0;
                }

                if (x < ScreenCenterX)
                {
                    TargetX = x - ScreenCenterX;
                    TargetX /= AimSpeed;
                    if (TargetX + ScreenCenterX < 0) TargetX = 0;
                }
            }

            //Y Axis

            if (y != 0)
            {
                if (y > ScreenCenterY)
                {
                    TargetY = -(ScreenCenterY - y);
                    TargetY /= AimSpeed;
                    if (TargetY + ScreenCenterY > ScreenCenterY * 2) TargetY = 0;
                }

                if (y < ScreenCenterY)
                {
                    TargetY = y - ScreenCenterY;
                    TargetY /= AimSpeed;
                    if (TargetY + ScreenCenterY < 0) TargetY = 0;
                }
            }

            if (!smooth)
            {
                MainForm.mouse_event(1, (int)TargetX, (int)(TargetY), 0, UIntPtr.Zero);
                return;
            }

            TargetX /= 10;
            TargetY /= 10;

            if (Math.Abs(TargetX) < 1)
            {
                if (TargetX > 0)
                {
                    TargetX = 1;
                }
                if (TargetX < 0)
                {
                    TargetX = -1;
                }
            }
            if (Math.Abs(TargetY) < 1)
            {
                if (TargetY > 0)
                {
                    TargetY = 1;
                }
                if (TargetY < 0)
                {
                    TargetY = -1;
                }
            }
            MainForm.mouse_event(1, (int)TargetX, (int)(TargetY), 0, UIntPtr.Zero);
        }

        private void _window_SizeChanged(object sender, OverlaySizeEventArgs e)
        {
            if (_graphics == null) return;

            if (_graphics.IsInitialized)
            {
                _graphics.Resize(e.Width, e.Height);
            }
            else
            {
                _graphics.Width = e.Width;
                _graphics.Height = e.Height;
            }
        }

        private void DrawShadowText(Graphics gfx, Font font, IBrush brush, Point pt, string txt)
        {
            var bpPt = new Point(pt.X - 1, pt.Y + 1);
            //var bpPt2 = new Point(pt.X + 1, pt.Y - 1);
            gfx.DrawText(font, _txtBrush, bpPt, txt);
            //gfx.DrawText(font, _txtBrush, bpPt2, txt);
            gfx.DrawText(font, brush, pt, txt);

        }
        private void DrawShadowText(Graphics gfx, Font font, float fontSize, IBrush brush, Point pt, string txt)
        {
            var bpPt = new Point();
            bpPt.X = pt.X - 1;
            bpPt.Y = pt.Y + 1;
            gfx.DrawText(font, fontSize, _txtBrush, bpPt, txt);
            gfx.DrawText(font, fontSize, brush, pt, txt);
        }
    }

}
