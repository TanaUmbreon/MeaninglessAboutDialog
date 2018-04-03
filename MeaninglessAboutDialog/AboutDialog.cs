using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using MeaninglessAboutDialog.Common;
using MeaninglessAboutDialog.Properties;

namespace MeaninglessAboutDialog
{
    public partial class AboutDialog : Form
    {
        public AboutDialog()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        #region 蛇足的な処理だが全力でコーディングした。反省はしていない。

        #region メインフレーム

        /// <summary>フレーム レート (FPS) 制御オブジェクト</summary>
        private FrameRateController controller = null;
        /// <summary>描画デバイス</summary>
        private GraphicsDevice device = null;
        /// <summary>タスク マネージャー</summary>
        private TaskManager manager = null;
        /// <summary>乱数ジェネレーター</summary>
        private Random rand = null;

        // 初期化
        private void AboutDialog_Load(object sender, EventArgs e)
        {
            // ウィンドウサイズはここで指定する
            ClientSize = new Size(width: 400, height: 225);

            // フィールドの初期化
            controller = new FrameRateController(owner: this, fps: 60);
            device = new GraphicsDevice(this);
            manager = new TaskManager(capacity: 50);
            rand = new Random();

            // 固定表示するタスクを生成
            manager.Add(new Background());
            manager.Add(new PanelLayer(color: Color.FromArgb(192, 128, 128, 128), x: 16.0f, y: 32.0f, width: 368.0f, height: 96.0f, priority: 0.9f));
            manager.Add(new IconLayer(x: 32.0f, y: 48.0f, priority: 0.95f));
            manager.Add(new TextLayer(text: AssemblyInfo.Title, emSize: 10.0f, color: Color.White, x: 112.0f, y: 48.0f, priority: 0.95f));
            manager.Add(new TextLayer(text: $"Version {AssemblyInfo.Version.ToString(3)}", emSize: 10.0f, color: Color.White, x: 112.0f, y: 70.0f, priority: 0.95f));
            manager.Add(new TextLayer(text: AssemblyInfo.Copyright, emSize: 10.0f, color: Color.White, x: 112.0f, y: 92.0f, priority: 0.95f));

            // メインループの実行
            controller.RunAsync(() =>
            {
                Update();
                Draw();
            });
        }

        // 終了処理
        private void AboutDialog_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (manager != null) manager.Dispose();
            if (device != null) device.Dispose();
        }

        /// <summary>
        /// メインフレームの状態を更新します。
        /// </summary>
        new private void Update()
        {
            // 毎フレーム一定確率でエフェクトを生成する
            if (rand.Next(256) < 8)
            {
                float x0 = rand.Next(ClientSize.Width);
                float y0 = -34.0f;
                float angle = (float)((Math.PI * rand.NextDouble() / 2.0) + (Math.PI / 4.0));
                float angleSpeed = 0.0f;
                float speed = (float)(rand.NextDouble() / 3.0) + 0.3f;
                float acceleration = 0.0f;
                float imageAngle = 0.0f;
                float imageAngleSpeed = ((float)rand.NextDouble() * 0.5f) + 0.5f;
                manager.Add(new EffectImage(x0, y0, angle, angleSpeed, speed, acceleration, imageAngle, imageAngleSpeed, 0.5f));
            }

            manager.Update();
        }

        /// <summary>
        /// メインフレームの状態を描画します。
        /// </summary>
        private void Draw()
        {
            Graphics g = device.BufferGraphics;

            // 画面の初期化
            g.Clear(Color.Black);

            // 全てのタスクを描画
            manager.Draw(g);

            // システムの情報を表示 ※タスクの実装をあきらめたので
            g.DrawString(controller.ActualFps.ToString("00.0") + " fps // " + controller.ElapsedFrame + " frames",
                Font, Brushes.White, 12.0f, 184.0f);
            g.DrawString(manager.Count + " tasks",
                Font, Brushes.White, 12.0f, 200.0f);

            // フォームに描画した画面を表示
            device.Draw();
        }

        private void AboutDialog_Paint(object sender, PaintEventArgs e)
        {
            device.Draw();
        }

        #endregion

        #region FrameRateController クラス

        /// <summary>
        /// フレーム レート (FPS) を制御するクラスです。
        /// </summary>
        // ウィンドウサイズを大きくすると描画が重たくなり、FPSが安定しないので
        //  ／￣＼
        // | ＾o＾| ＜ やめてください　しんでしまいます
        //  ＼＿／
        // 
        // ※この場合は素直にDirect Xとかでびょうがしてください　おねがいします
        // でもウィンドウが小さければ120fpsとかでも平気で出せます怖いね
        private class FrameRateController
        {
            /// <summary>インスタンスを所持するオーナー コントロール</summary>
            private Control owner = null;
            /// <summary>フレーム レート制御を行っているを示すフラグ</summary>
            private bool isRunning = false;

            /// <summary>
            /// 設定されているフレーム レートを取得します。
            /// </summary>
            public int Fps { get; private set; }

            /// <summary>
            /// 実際のフレーム レートを取得します。
            /// </summary>
            public float ActualFps { get; private set; }

            /// <summary>
            /// <see cref="ActualFps" /> プロパティの更新間隔をフレーム単位で取得または設定します。
            /// </summary>
            public int ActualFpsUpdateSpan { get; set; }

            /// <summary>
            /// 経過したフレーム数を取得します。
            /// </summary>
            public int ElapsedFrame { get; private set; }

            /// <summary>
            /// <see cref="FrameRateController"/> の新しいインスタンスを生成します。
            /// </summary>
            /// <param name="owner">インスタンスを所持するオーナー コントロール。</param>
            /// <param name="fps">設定するフレーム レート。省略時は 30 が設定されます。</param>
            public FrameRateController(Control owner, int fps = 30)
            {
                if (fps <= 0) throw new ArgumentOutOfRangeException(
                    "fps", "FPS をゼロ以下にすることはできません。");

                this.Fps = fps;
                ElapsedFrame = 0;
                ActualFps = (float)fps;
                ActualFpsUpdateSpan = fps / 2;
                if (ActualFpsUpdateSpan <= 0) ActualFpsUpdateSpan = 1;

                this.owner = owner;
            }

            /// <summary>
            /// フレームレートの制御をバックグラウンドで実行します。1フレーム分の時間が経過すると <see cref="callbackMethod"/> が呼び出されます。
            /// </summary>
            /// <param name="callbackMethod">毎フレーム呼び出されるメソッド。</param>
            public void RunAsync(Action callbackMethod)
            {
                // 既にメソッドを実行していた場合はタスクを作らない
                if (isRunning) return;

                // フレームレート制御用のタスクを作成して実行
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    List<int> timeList = new List<int>(Fps); // 1秒が経過するまでの各フレームの実時間リスト
                    List<int> history = new List<int>(Fps); // 処理時間履歴リスト
                    Stopwatch watch = new Stopwatch(); // 時間計測用

                    // 時間リストを作成
                    for (int i = 0; i < Fps; i++)
                    {
                        int time1 = (int)Math.Round((double)(1000 * i) / (double)Fps);
                        int time2 = (int)Math.Round((double)(1000 * (i + 1)) / (double)Fps);
                        int time = time2 - time1;
                        timeList.Add(time);
                        history.Add(time);
                    }

                    long before = watch.ElapsedMilliseconds; // 処理前の経過時間
                    long totalError = 0L; // 誤差の累計
                    const long MaxError = 8L; // 1フレームあたりに補正する誤差
                    long maxTotalError = MaxError * Fps; // 誤差の累計の最大値

                    watch.Start();
                    while (true)
                    {
                        // 更新処理を呼び出し
                        try { owner.Invoke(callbackMethod); }
                        catch (Exception) { break; }

                        // 更新処理後の時間を計算
                        long time = timeList[ElapsedFrame % Fps]; // 1フレームの実時間
                        long procTime = watch.ElapsedMilliseconds - before; // 処理時間
                        long correct = totalError; // 補正時間

                        if (totalError > MaxError) { correct = MaxError; }
                        else if (totalError < -MaxError) { correct = -MaxError; }

                        long wait = time - procTime + correct; // 待機時間

                        // 必要な時間だけスリープ
                        if (wait > 0L) Thread.Sleep((int)wait);

                        // スリープ後の経過時間から誤差を計算
                        long after = watch.ElapsedMilliseconds; // 処理後の経過時間
                        procTime = after - before;
                        totalError += time - procTime;
                        if (totalError > maxTotalError) { totalError = maxTotalError; }
                        else if (totalError < -maxTotalError) { totalError = -maxTotalError; }

                        // 実際のフレームレートを計算
                        history[ElapsedFrame % Fps] = (int)procTime;
                        if ((ElapsedFrame % ActualFpsUpdateSpan) == 0)
                        {
                            ActualFps = (float)(1000 * Fps) / (float)history.Sum();
                        }

                        before = after;
                        ElapsedFrame++;
                    }
                });

                isRunning = true;
            }
        }

        #endregion

        #region GraphicsDevice クラス

        /// <summary>
        /// コントロールへの描画機能を提供するデバイス クラスです。
        /// </summary>
        // ダブル バッファリングの仕組みを使っているので画面のちらつきが無くなるお。
        // Form クラスの DoubleBuffered プロパティなんて使い物になりません()
        private class GraphicsDevice : IDisposable
        {
            /// <summary>描画対象のコントロール</summary>
            private Control drawingTarget;
            /// <summary>描画バッファ</summary>
            private Bitmap buffer = null;

            /// <summary>バッファへの描画オブジェクトを取得します。</summary>
            public Graphics BufferGraphics { get; private set; }

            /// <summary>
            /// <see cref="GraphicsDevice" /> の新しいインスタンスを生成します。
            /// </summary>
            /// <param name="drawingTarget">描画対象となるコントロール。</param>
            public GraphicsDevice(Control drawingTarget)
            {
                this.drawingTarget = drawingTarget;
                buffer = new Bitmap(drawingTarget.ClientSize.Width, drawingTarget.ClientSize.Height);
                BufferGraphics = Graphics.FromImage(buffer);
            }

            /// <summary>
            /// インスタンスのリソースを破棄します。
            /// </summary>
            protected virtual void Dispose(bool disposing)
            {
                if (!disposed)
                {
                    if (disposing)
                    {
                        if (BufferGraphics != null) BufferGraphics.Dispose();
                        if (buffer != null) buffer.Dispose();
                    }
                }
                disposed = true;
            }

            #region IDisposable

            /// <summary>Dispose メソッドを実行したことを示す値</summary>
            private bool disposed = false;

            /// <summary>
            /// インスタンスのリソースを破棄します。
            /// </summary>
            public void Dispose()
            {
                // マネージリソースおよびアンマネージリソースの解放
                Dispose(true);
                // ガベージコレクションによる、このオブジェクトのデストラクタ呼び出しを禁止
                GC.SuppressFinalize(this);
            }

            /// <summary>
            /// ファイナライザ (デストラクタ)
            /// </summary>
            ~GraphicsDevice()
            {
                // アンマネージリソースの解放
                Dispose(false);
            }

            #endregion

            /// <summary>
            /// コントロールに現在のバッファを描画します。
            /// </summary>
            public void Draw()
            {
                using (Graphics g = drawingTarget.CreateGraphics())
                {
                    g.DrawImage(buffer, Point.Empty);
                }
            }
        }

        #endregion

        #region Task クラス

        /// <summary>
        /// 更新処理と描画処理の機能を提供します。
        /// </summary>
        // ゲームプログラミングでいうところの「タスク」
        // .NET Framework の System.Threading.Tasks 名前空間にある Task クラスと無縁なので注意！
        // 「タスク」については説明を書くのが面倒なので知りたければググってください
        private abstract class Task
        {
            /// <summary>
            /// このタスクを破棄できることを示すフラグを取得します。
            /// </summary>
            public bool CanDelete { get; protected set; }

            /// <summary>
            /// 更新処理・描画処理の実行順序を表す優先度を取得します。
            /// </summary>
            public float Priority { get; private set; }

            /// <summary>
            /// <see cref="Task"/> のコンストラクタ。
            /// </summary>
            /// <param name="priority">更新処理・描画処理の実行順序を表す優先度。0.0f ～ 1.0f の範囲で指定し、値が小さい方から処理が行われます。</param>
            /// <remarks>リソースの読み込みはコンストラクタで行わず、<see cref="Initialize"/> メソッドで行います。</remarks>
            public Task(float priority = 0.5f)
            {
                if (priority < 0.0f) priority = 0.0f;
                else if (priority > 1.0f) priority = 1.0f;

                CanDelete = false;
                this.Priority = priority;
            }

            /// <summary>
            /// 初期化処理を行います。
            /// </summary>
            /// <remarks><see cref="IDisposable"/> インターフェイスを実装するオブジェクトの生成はこのメソッドで行います。</remarks>
            public virtual void Initialize() { }

            /// <summary>
            /// 終了処理を行います。
            /// </summary>
            /// <remarks><see cref="IDisposable"/> インターフェイスを実装するオブジェクトの解放はこのメソッドで行います。</remarks>
            public virtual void Destruct() { }

            /// <summary>
            /// 更新処理を行います。
            /// </summary>
            public virtual void Update() { }

            /// <summary>
            /// 描画処理を行います。
            /// </summary>
            /// <param name="g">描画オブジェクト。</param>
            public virtual void Draw(Graphics g) { }
        }

        #endregion

        #region TaskManager クラス

        /// <summary>
        /// タスクの初期化・更新・描画・終了処理を一元管理するクラスです。
        /// </summary>
        // .NET Framework のクラスライブラリは便利だなぁ……。
        // 連結リストとか自作しないで済むし。
        private class TaskManager : IDisposable
        {
            /// <summary>管理するタスクのリスト</summary>
            private LinkedList<Task> tasks;

            /// <summary>
            /// 管理できるタスクの許容数を取得します。
            /// </summary>
            public int Capacity { get; private set; }

            /// <summary>
            /// 現在管理しているタスクの数を取得します。
            /// </summary>
            public int Count
            {
                get { return tasks.Count; }
            }

            /// <summary>
            /// <see cref="TaskManager"/> の新しいインスタンスを生成します。
            /// </summary>
            /// <param name="capacity">管理するタスクの許容数。</param>
            public TaskManager(int capacity)
            {
                // エラーチェック
                if (capacity < 0) throw new ArgumentOutOfRangeException(
                     "capacity", "許容数をゼロ未満にすることはできません。");

                this.Capacity = capacity;
                tasks = new LinkedList<Task>();
            }

            /// <summary>
            /// インスタンスのリソースを破棄します。
            /// </summary>
            protected virtual void Dispose(bool disposing)
            {
                if (!disposed)
                {
                    if (disposing)
                    {
                        foreach (var actor in tasks) actor.Destruct();
                    }
                }
                disposed = true;
            }

            #region IDisposable

            /// <summary>Dispose メソッドを実行したことを示す値</summary>
            private bool disposed = false;

            /// <summary>
            /// インスタンスのリソースを破棄します。
            /// </summary>
            public void Dispose()
            {
                // マネージリソースおよびアンマネージリソースの解放
                Dispose(true);
                // ガベージコレクションによる、このオブジェクトのデストラクタ呼び出しを禁止
                GC.SuppressFinalize(this);
            }

            /// <summary>
            /// ファイナライザ (デストラクタ)
            /// </summary>
            ~TaskManager()
            {
                // アンマネージリソースの解放
                Dispose(false);
            }

            #endregion

            /// <summary>
            /// 管理するタスクを追加します。上限に達している場合は追加されません。
            /// </summary>
            /// <param name="task">追加するタスク。</param>
            /// <returns>引数に渡したタスク。上限に達して追加されなかった場合は null。</returns>
            public Task Add(Task task)
            {
                // 許容数を超えている場合は追加しない
                if (tasks.Count >= Capacity) return null;

                // 追加できる場合はタスクを初期化
                task.Initialize();

                // タスクの描画優先度順に並ぶようリストに追加
                var node = tasks.First;
                while (node != null)
                {
                    if (task.Priority < node.Value.Priority)
                    {
                        tasks.AddBefore(node, task);
                        return task;
                    }
                    node = node.Next;
                }
                tasks.AddLast(task);
                return task;
            }

            /// <summary>
            /// タスクの更新処理を行います。
            /// </summary>
            public void Update()
            {
                // 更新処理
                var node = tasks.First;
                while (node != null)
                {
                    node.Value.Update();
                    node = node.Next;
                }

                // 破棄フラグ確認・終了処理
                node = tasks.First;
                while (node != null)
                {
                    if (node.Value.CanDelete)
                    {
                        var delNode = node;
                        delNode.Value.Destruct();

                        node = node.Next;

                        tasks.Remove(delNode);
                    }
                    node = node.Next;
                }
            }

            /// <summary>
            /// タスクの描画処理を行います。
            /// </summary>
            /// <param name="g"></param>
            public void Draw(Graphics g)
            {
                var node = tasks.First;
                while (node != null)
                {
                    node.Value.Draw(g);
                    node = node.Next;
                }
            }
        }

        #endregion

        #region Background クラス

        /// <summary>
        /// 背景描画用のタスククラス。
        /// </summary>
        private class Background : Task
        {
            /// <summary>
            /// <see cref="Background"/> クラスの新しいインスタンスを生成します。
            /// </summary>
            // 背景だから真っ先に描画させるお (優先度: 0.0f で固定)
            public Background() : base(0.0f) { }

            public override void Draw(Graphics g)
            {
                Color c1 = Color.FromArgb(0, 0, 32);
                Color c2 = Color.FromArgb(48, 48, 72);

                using (LinearGradientBrush b = new LinearGradientBrush(
                    g.VisibleClipBounds, c1, c2, LinearGradientMode.Vertical))
                {
                    g.FillRectangle(b, g.VisibleClipBounds);
                }
            }
        }

        #endregion

        #region TextLayer クラス

        /// <summary>
        /// 文字列描画用のタスククラス。
        /// </summary>
        private class TextLayer : Task
        {
            private string text;
            private float emSize;
            private Color color;
            private float x;
            private float y;
            private Font font;
            private SolidBrush brush;

            /// <summary>
            /// <see cref="TextLayer"/> クラスの新しいインスタンスを生成します。
            /// </summary>
            public TextLayer(string text, float emSize, Color color, float x, float y, float priority)
                : base(priority)
            {
                this.text = text;
                this.emSize = emSize;
                this.color = color;
                this.x = x;
                this.y = y;
            }

            /// <summary>
            /// 初期化処理を行います。
            /// </summary>
            /// <remarks>リソースの読み込みなどはこのメソッドで行います。</remarks>
            public override void Initialize()
            {
                brush = new SolidBrush(color);
                font = new Font("メイリオ", emSize);
            }

            /// <summary>
            /// 終了処理を行います。
            /// </summary>
            /// <remarks>リソースの破棄などはこのメソッドで行います。</remarks>
            public override void Destruct()
            {
                if (brush != null) brush.Dispose();
                if (font != null) font.Dispose();
            }

            public override void Draw(Graphics g)
            {
                g.DrawString(text, font, brush, x, y);
            }
        }

        #endregion

        #region PanelLayer クラス

        /// <summary>
        /// 単色の矩形を描画するタスククラス。
        /// </summary>
        private class PanelLayer : Task
        {
            private Color color;
            private float x;
            private float y;
            private float width;
            private float height;
            private SolidBrush brush;

            /// <summary>
            /// <see cref="PanelLayer"/> クラスの新しいインスタンスを生成します。
            /// </summary>
            public PanelLayer(Color color, float x, float y, float width, float height, float priority)
                : base(priority)
            {
                this.color = color;
                this.x = x;
                this.y = y;
                this.width = width;
                this.height = height;
            }

            /// <summary>
            /// 初期化処理を行います。
            /// </summary>
            /// <remarks>リソースの読み込みなどはこのメソッドで行います。</remarks>
            public override void Initialize()
            {
                brush = new SolidBrush(color);
            }

            /// <summary>
            /// 終了処理を行います。
            /// </summary>
            /// <remarks>リソースの破棄などはこのメソッドで行います。</remarks>
            public override void Destruct()
            {
                if (brush != null) brush.Dispose();
            }

            public override void Draw(Graphics g)
            {
                g.FillRectangle(brush, x, y, width, height);
            }
        }

        #endregion

        #region IconLayer クラス

        /// <summary>
        /// アイコン画像を描画するタスククラス。
        /// </summary>
        private class IconLayer : Task
        {
            private float x;
            private float y;

            /// <summary>
            /// <see cref="PanelLayer"/> クラスの新しいインスタンスを生成します。
            /// </summary>
            public IconLayer(float x, float y, float priority)
                : base(priority)
            {
                this.x = x;
                this.y = y;
            }
            public override void Draw(Graphics g)
            {
                g.DrawImage(Resources.AppIcon48x, x, y);
            }
        }

        #endregion

        #region EffectImage クラス

        /// <summary>
        /// 専用のエフェクトを描画するタスククラス。
        /// </summary>
        // この子がメインと言っても過言ではない
        private class EffectImage : Task
        {
            /// <summary>フェードアウトを開始するフレーム数</summary>
            private const int BeginFadeOutFrameCount = 540;
            /// <summary>フェードアウトにかかるフレーム数</summary>
            private const int FadeOutFrameSpan = 60;
            /// <summary>経過フレーム数</summary>
            private int frameCount;
            private Image image;
            /// <summary>描画する画像のアルファ値</summary>
            private float alpha;
            private float halfWidth;
            private float halfHeight;

            private float x0;
            private float y0;
            private float angle;
            private float angleSpeed;
            private float speed;
            private float acceleration;
            private float imageAngle;
            private float imageAngleSpeed;

            /// <summary>
            /// <see cref="EffectImage"/> の新しいインスタンスを生成します。
            /// </summary>
            /// <param name="x0">X方向の中心位置。</param>
            /// <param name="y0">Y方向の中心位置。</param>
            /// <param name="angle">ラジアン単位の移動方向。</param>
            /// <param name="angleSpeed">移動方向の角速度。</param>
            /// <param name="speed">速さ。</param>
            /// <param name="acceleration">加速度。</param>
            /// <param name="imageAngle">ラジアン単位の画像の向き。</param>
            /// <param name="imageAngleSpeed">画像の向きの角速度。</param>
            /// <param name="priority">描画の順番を制御する優先度。</param>
            public EffectImage(float x0, float y0, float angle, float angleSpeed, float speed, float acceleration, float imageAngle, float imageAngleSpeed, float priority)
                : base(priority)
            {
                frameCount = 0;
                this.x0 = x0;
                this.y0 = y0;
                this.angle = angle;
                this.angleSpeed = angleSpeed;
                this.speed = speed;
                this.acceleration = acceleration;
                this.imageAngle = imageAngle;
                this.imageAngleSpeed = imageAngleSpeed;
            }

            public override void Initialize()
            {
                image = Resources.Effect0;
                alpha = 1.0f;
                halfWidth = image.Width / 2.0f;
                halfHeight = image.Height / 2.0f;
            }

            public override void Destruct()
            {

            }

            public override void Update()
            {
                // 位置の移動
                x0 += (float)Math.Cos(angle) * speed;
                y0 += (float)Math.Sin(angle) * speed;

                // 移動後に加速度と角速度を補正
                angle += angleSpeed;
                speed += acceleration;
                imageAngle += imageAngleSpeed;

                // 削除判定
                if (frameCount > BeginFadeOutFrameCount)
                {
                    int fadeCount = frameCount - BeginFadeOutFrameCount;

                    if (fadeCount > FadeOutFrameSpan)
                    {
                        CanDelete = true;
                        return;
                    }

                    alpha = 1.0f - ((float)fadeCount / (float)FadeOutFrameSpan);
                    if (alpha < 0.0f) alpha = 0.0f;
                    else if (alpha > 1.0f) alpha = 1.0f;
                }

                frameCount++;
            }

            public override void Draw(Graphics g)
            {
                // 画像の中央を原点にして、画像を回転させて描画
                g.TranslateTransform(-x0 - halfWidth, -y0 - halfHeight);
                g.RotateTransform(imageAngle, MatrixOrder.Append);
                g.TranslateTransform(x0, y0, MatrixOrder.Append);
                if (alpha < 1.0f)
                {
                    using (Bitmap img = new Bitmap(image))
                    {
                        ColorMatrix cm = new ColorMatrix();
                        cm.Matrix00 = 1.0f;
                        cm.Matrix11 = 1.0f;
                        cm.Matrix22 = 1.0f;
                        cm.Matrix33 = alpha;
                        cm.Matrix44 = 1.0f;

                        ImageAttributes ia = new ImageAttributes();
                        ia.SetColorMatrix(cm);

                        g.DrawImage(img, new Rectangle((int)x0, (int)y0, img.Width, img.Height),
                            0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);
                    }
                }
                else
                {
                    g.DrawImage(image, x0, y0, image.Width, image.Height);
                }
                g.ResetTransform();
            }
        }

        #endregion

        #endregion
    }
}
