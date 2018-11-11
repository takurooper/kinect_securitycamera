/*本プログラム
 * 5E13 畔柳拓実
 * 2018/02/26
 * ver10.0
 * 完成版
 * */


using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;
using Microsoft.Kinect.VisualGestureBuilder;

//Kinect for Windows SDK v2を使用することを名前空間を指定して示す
namespace KinectV2
{
    public partial class MainWindow : Window
    {
        ////////////////////////////////////////////////////変数宣言////////////////////////////////////////////////////
        int mode;
        // Kinectを開く
        KinectSensor kinect;
        CoordinateMapper mapper;
        MultiSourceFrameReader multiReader;
        FrameDescription colorFrameDesc;
        ColorImageFormat colorFormat = ColorImageFormat.Bgra;
        //3つを同時に処理するため、個別のリーダーではなくMultiを使う
        byte[] colorBuffer;
        FrameDescription depthFrameDesc;
        ushort[] depthBuffer;
        byte[] bodyIndexBuffer;
        FrameDescription bodyFrameDesc;
        private Body[] bodies;
        int BODY_COUNT;
        //Kinectが瞬間に捉えている人の数
        int BODY_CAPTURE =0; 
        //Visual Gesture Builder 
        private VisualGestureBuilderDatabase gestureDatabase;
        private VisualGestureBuilderFrameSource gestureFrameSource;
        private VisualGestureBuilderFrameReader gestureFrameReader;
        private Gesture stare;
        private Gesture wander;

        ////////////////////////////////////////////////////初期化////////////////////////////////////////////////////
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                kinect = KinectSensor.GetDefault();
                if (kinect == null) throw new Exception("Kinect v2を開けません");
                kinect.Open();
                //CoordinateMapperの取得
                mapper = kinect.CoordinateMapper;
                // カラー画像の情報を作成する(BGRAフォーマット)
                colorFrameDesc = kinect.ColorFrameSource.CreateFrameDescription(colorFormat);
                //ここからがリーダーを開くところに対応
                colorBuffer = new byte[colorFrameDesc.LengthInPixels * colorFrameDesc.BytesPerPixel];
                depthFrameDesc = kinect.DepthFrameSource.FrameDescription;
                depthBuffer = new ushort[depthFrameDesc.LengthInPixels];
                var bodyIndexFrameDesc = kinect.BodyIndexFrameSource.FrameDescription;
                bodyIndexBuffer = new byte[bodyIndexFrameDesc.LengthInPixels];
                BODY_COUNT = kinect.BodyFrameSource.BodyCount;
                bodies = new Body[BODY_COUNT];

                // フレームリーダーを開く
                multiReader = kinect.OpenMultiSourceFrameReader(
                    FrameSourceTypes.Color |
                    FrameSourceTypes.Depth |
                    FrameSourceTypes.BodyIndex|
                    FrameSourceTypes.Body);

                multiReader.MultiSourceFrameArrived += multiReader_MultiSourceFrameArrived;
                InitializeGesture();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Close();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (multiReader != null)
            {
                multiReader.MultiSourceFrameArrived -= multiReader_MultiSourceFrameArrived;
                multiReader.Dispose();
                multiReader = null;
            }

            if (kinect != null)
            {
                kinect.Close();
                kinect = null;
            }
        }

        void InitializeGesture()
        {
            //ジェスチャーデータベースの初期設定、読み込み
            gestureDatabase = new VisualGestureBuilderDatabase("dangerousmoves.gbd");
            gestureFrameSource = new VisualGestureBuilderFrameSource(kinect, 0);
            // 使用するジェスチャーをデータベースから取り出す
            foreach (var gesture in gestureDatabase.AvailableGestures)
            {
                if (gesture.Name == "wander") wander = gesture;
                else if (gesture.Name == "stare") stare = gesture;
                this.gestureFrameSource.AddGesture(gesture);
            }
            // ジェスチャーリーダーを開く
            gestureFrameReader = gestureFrameSource.OpenReader();
            gestureFrameReader.IsPaused = true;
            gestureFrameReader.FrameArrived += gestureFrameReader_FrameArrived;
            
        }

        void multiReader_MultiSourceFrameArrived(object sender,
                                MultiSourceFrameArrivedEventArgs e)
        {
            var multiFrame = e.FrameReference.AcquireFrame(); // マルチフレームを取得する
            if (multiFrame == null)return;

            ////////////////////////////////////////////////////各種データ取得////////////////////////////////////////////////////
            UpdateColorFrame(multiFrame);
            UpdateBodyIndexFrame(multiFrame);
            UpdateDepthFrame(multiFrame);
            UpdateBodyFrame(multiFrame);

            if (BODY_CAPTURE != 0)
            {
                TextBlock7.Visibility = System.Windows.Visibility.Visible;
                TextBlock7.Text = BODY_CAPTURE.ToString() + "人検出";
            }

            if (!gestureFrameSource.IsTrackingIdValid)
            {
                using (BodyFrame bodyFrame = multiFrame.BodyFrameReference.AcquireFrame())
                {
                    if (bodyFrame != null)
                    {
                        bodyFrame.GetAndRefreshBodyData(bodies);
                        foreach (var body in bodies)
                        {
                            if (body != null && body.IsTracked)
                            {
                                // ジェスチャー判定対象としてbodyを選択
                                gestureFrameSource.TrackingId = body.TrackingId;
                                // ジェスチャー判定開始
                                gestureFrameReader.IsPaused = false;
                                break;
                            }
                        }
                    }
                }
            }
            draw();
        }

        ////////////////////////////////////////////////////ジェスチャー処理////////////////////////////////////////////////////
        private void gestureFrameReader_FrameArrived(object sender, VisualGestureBuilderFrameArrivedEventArgs e)
        {
            using (var gestureFrame = e.FrameReference.AcquireFrame())
            {
                // stare（監視カメラを見つめる）ジェスチャーの判定結果がある場合
                if (gestureFrame != null && gestureFrame.DiscreteGestureResults != null&& gestureFrame.DiscreteGestureResults[stare] != null)
                {
                    var stareresult = gestureFrame.DiscreteGestureResults[stare];
                    if (stareresult.Detected ==true)
                    {
                        //ジェスチャーの検出がDetectedの場合、警告文の表示
                        TextBlock1.Visibility = System.Windows.Visibility.Visible;
                        TextBlock1.Text = "危険動作検出｜stare";
                        TextBlock7.Text = BODY_CAPTURE.ToString() + "人検出";
                        mode = 1;
                    }
                    else
                    {
                        TextBlock1.Text = "初期化";
                        TextBlock1.Visibility = System.Windows.Visibility.Hidden;
                        TextBlock2.Visibility = System.Windows.Visibility.Hidden;
                        TextBlock3.Visibility = System.Windows.Visibility.Hidden;
                        TextBlock4.Visibility = System.Windows.Visibility.Hidden;
                        TextBlock5.Visibility = System.Windows.Visibility.Hidden;
                        TextBlock6.Visibility = System.Windows.Visibility.Hidden;
                        ProgressBar1.Visibility = System.Windows.Visibility.Hidden;
                        ProgressBar2.Visibility = System.Windows.Visibility.Hidden;
                        ProgressBar3.Visibility = System.Windows.Visibility.Hidden;
                        ProgressBar4.Visibility = System.Windows.Visibility.Hidden;
                        ProgressBar5.Visibility = System.Windows.Visibility.Hidden;
                        ProgressBar6.Visibility = System.Windows.Visibility.Hidden;
                        mode = 0;
                    }
                }
                
                // wander（徘徊している）ジェスチャーの判定結果がある場合
                if (gestureFrame != null && gestureFrame.ContinuousGestureResults != null && gestureFrame.ContinuousGestureResults[wander] != null)
                {
                    var wanderresult = gestureFrame.ContinuousGestureResults[wander];
                    if (wanderresult.Progress >= 0.8)
                    {
                        //ジェスチャーの検出がTrueの場合、その進捗をプログレスバーに反映
                        TextBlock1.Visibility = System.Windows.Visibility.Visible;
                        ProgressBar1.Visibility = System.Windows.Visibility.Visible;
                        TextBlock1.Text = "危険動作検出｜wander";
                        TextBlock7.Text = BODY_CAPTURE.ToString() + "人検出";
                        var progressResult = gestureFrame.ContinuousGestureResults[wander];
                        ProgressBar1.Value = progressResult.Progress;
                        mode = 1;
                    }
                    else
                    {
                        TextBlock1.Text = "初期化";
                        TextBlock1.Visibility = System.Windows.Visibility.Hidden;
                        TextBlock2.Visibility = System.Windows.Visibility.Hidden;
                        TextBlock3.Visibility = System.Windows.Visibility.Hidden;
                        TextBlock4.Visibility = System.Windows.Visibility.Hidden;
                        TextBlock5.Visibility = System.Windows.Visibility.Hidden;
                        TextBlock6.Visibility = System.Windows.Visibility.Hidden;
                        ProgressBar1.Visibility = System.Windows.Visibility.Hidden;
                        ProgressBar2.Visibility = System.Windows.Visibility.Hidden;
                        ProgressBar3.Visibility = System.Windows.Visibility.Hidden;
                        ProgressBar4.Visibility = System.Windows.Visibility.Hidden;
                        ProgressBar5.Visibility = System.Windows.Visibility.Hidden;
                        ProgressBar6.Visibility = System.Windows.Visibility.Hidden;
                        mode = 0;
                    }
                }
            }
        }

        ////////////////////////////////////////////////////画面表示////////////////////////////////////////////////////
        private void UpdateColorFrame(MultiSourceFrame multiFrame)
        {
            using (var colorFrame = multiFrame.ColorFrameReference.AcquireFrame())
            {
                if (colorFrame == null)return;
                colorFrame.CopyConvertedFrameDataToArray(colorBuffer, colorFormat);
            }
        }

        private void UpdateDepthFrame(MultiSourceFrame multiFrame)
        {
            using (var depthFrame = multiFrame.DepthFrameReference.AcquireFrame())
            {
                if (depthFrame == null)return;
                depthFrame.CopyFrameDataToArray(depthBuffer);
            }
        }

        private void UpdateBodyIndexFrame(MultiSourceFrame multiFrame)
        {
            using (var bodyIndexFrame = multiFrame.BodyIndexFrameReference.AcquireFrame())
            {
                if (bodyIndexFrame == null)return;
                bodyIndexFrame.CopyFrameDataToArray(bodyIndexBuffer);
            }
            return;
        }

        private void UpdateBodyFrame(MultiSourceFrame multiFrame)
        {
            BODY_CAPTURE=0;
            BodyFrame bodyFrame;
                bodyFrame = multiFrame.BodyFrameReference.AcquireFrame();
                if (bodyFrame == null)return;
                bodyFrame.GetAndRefreshBodyData(bodies);
                //画面に表示されている人数を数える
                for (int count = 0; count < BODY_COUNT; count++)
                {
                    Body body = bodies[count];
                    bool tracked = body.IsTracked;
                    if (!tracked)
                    {
                        continue;
                    }
                ulong trackingId = body.TrackingId;
                    gestureFrameSource = gestureFrameReader.VisualGestureBuilderFrameSource;
                    gestureFrameSource.TrackingId = trackingId;
                BODY_CAPTURE++;
                }            
            bodyFrame.Dispose();
        }

        private void draw()
        {
            // Depth画像の解像度でデータを作る
            var colorImageBuffer = new byte[depthFrameDesc.LengthInPixels * colorFrameDesc.BytesPerPixel];
            // Depth座標系に対応するカラー座標系の一覧を取得する
            var colorSpace = new ColorSpacePoint[depthFrameDesc.LengthInPixels];
            mapper.MapDepthFrameToColorSpace(depthBuffer, colorSpace);
            // 並列で処理しながら描画
            Parallel.For(0, depthFrameDesc.LengthInPixels, i =>
            {
                int colorX = (int)colorSpace[i].X;
                int colorY = (int)colorSpace[i].Y;
                if ((colorX < 0) || (colorFrameDesc.Width <= colorX) || (colorY < 0) || (colorFrameDesc.Height <= colorY))return;
                // カラー座標系のインデックス
                int colorIndex = (colorY * colorFrameDesc.Width) + colorX;
                if (mode == 0)
                {
                    int bodyIndex = bodyIndexBuffer[i];
                    if (bodyIndex != 255)return;
                }
                // カラー画像を設定する
                int colorImageIndex = (int)(i * colorFrameDesc.BytesPerPixel);
                int colorBufferIndex = (int)(colorIndex * colorFrameDesc.BytesPerPixel);
                colorImageBuffer[colorImageIndex + 0] = colorBuffer[colorBufferIndex + 0];//Blue
                colorImageBuffer[colorImageIndex + 1] = colorBuffer[colorBufferIndex + 1];//Green
                colorImageBuffer[colorImageIndex + 2] = colorBuffer[colorBufferIndex + 2];//Red
            });
            ImageColor.Source = BitmapSource.Create(depthFrameDesc.Width, depthFrameDesc.Height, 96, 96,
                PixelFormats.Bgr32, null, colorImageBuffer,(int)(depthFrameDesc.Width * colorFrameDesc.BytesPerPixel));
        }
    }
}