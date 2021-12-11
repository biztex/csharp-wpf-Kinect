//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.SkeletonBasics
{


    using System.Net;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using System.Text;
    using System;
    using System.Windows.Controls;
    /// <まとめ>
    /// MainWindow.xamlのインタラクションロジック
    /// </まとめ>
    public partial class MainWindow : Window
    {
        /// <まとめ>
        /// 出力画面の幅
        /// </まとめ>
        private const float RenderWidth = 640.0f;

       
        /// 出力画面の高さ
      
        private const float RenderHeight = 480.0f;

        /// 描画されたジョイントラインの太さ
        private const double JointThickness = 3;

        /// 体の中心の楕円の厚さ
        private const double BodyCenterThickness = 10;

        /// クリップエッジの長方形の厚さ
		
        private const double ClipBoundsThickness = 10;

        /// スケルトンの中心点を描画するために使用されるブラシ
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// 現在追跡されているジョイントの描画に使用されるブラシ
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// 現在推測されているジョイントの描画に使用されるブラシ        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// 現在追跡されているボーンの描画に使用されるペン
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// 現在推測されている骨を描くために使用されるペン
		
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// アクティブKinectセンサー
        private KinectSensor sensor;

        /// スケルトンレンダリング出力の描画グループ
        private DrawingGroup drawingGroup;

        /// 表示する描画画像
        private DrawingImage imageSource;

        /// MainWindowクラスの新しいインスタンスを初期化します。


        Image userImage = new Image();

        ///ラインフラグ変数
        /// Line通知を1回だけ送信するために使用
        Boolean i = false;
        Boolean j = false;
        public MainWindow()
        {
            InitializeComponent();
        }

        ///どのエッジがスケルトンデータをクリッピングしているかを示すインジケーターを描画します
      
        /// <paramname = "skeleton">クリッピング情報を描画するスケルトン</ param>
        /// <paramname = "drawingContext">描画先の描画コンテキスト</ param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

       
        ///スタートアップタスクを実行する
     
        /// <paramname = "sender">イベントを送信するオブジェクト</ param>
        /// <paramname = "e">イベント引数</ param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // 描画に使用する描画グループを作成します
            this.drawingGroup = new DrawingGroup();

            // 画像コントロールで使用できる画像ソースを作成します
            this.imageSource = new DrawingImage(this.drawingGroup);

            // 画像コントロールを使用して図面を表示します
            Image.Source = this.imageSource;

            // すべてのセンサーを調べて、最初に接続されたものを起動します。
            // これには、アプリ起動時にKinectが接続されている必要があります。
            // アプリをプラグ/アンプラグに強くするためには 
            // Microsoft.Kinect.Toolkitで提供されているKinectSensorChooserを使用することをお勧めします（Toolkit Browserのコンポーネントを参照）。
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();

                // 新しいカラーフレームデータがあるたびに呼び出されるイベントハンドラを追加する
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                // センサーを起動します。!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }

        /// シャットダウンタスクの実行
		
        /// <param name="sender">イベントを送信するオブジェクト</param>
        /// <param name="e">イベント引数</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        
        /// KinectセンサーのSkeletonFrameReadyイベントのイベントハンドラー
        
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // レンダリングサイズを設定するために透明な背景を描く
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                // レンダリングエリア外への描画を防ぐ
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

      
        /// スケルトンの骨や関節を描く
      

		/// <param name="skeleton">骨格を描く</param>
        /// <param name="drawingContext">描画先のコンテキスト</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // トルソーのレンダリング
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // 左腕
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // 右腕
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // 左足
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // 右足
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            // レンダージョイント
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }




			//肘と足首、膝、肩部位の座標入力
            

            SkeletonPoint headPoint         = skeleton.Joints[JointType.ShoulderCenter].Position;	//肩
            SkeletonPoint ElbowLeftPoint    = skeleton.Joints[JointType.ElbowLeft].Position;		//肘
            SkeletonPoint ElbowRightPoint   = skeleton.Joints[JointType.ElbowRight].Position;		//肘
            SkeletonPoint AnkleLeft         = skeleton.Joints[JointType.AnkleLeft].Position;		//足首
            SkeletonPoint AnkleRight         = skeleton.Joints[JointType.AnkleRight].Position;		//足首
            SkeletonPoint KneeRightPoint    = skeleton.Joints[JointType.KneeRight].Position;		//膝
            SkeletonPoint KneeLeftPoint    = skeleton.Joints[JointType.KneeLeft].Position;			//膝
            			


			
            ///足首が膝よりも高いところにある場合
			if(AnkleLeft.Y > KneeRightPoint.Y || AnkleRight.Y > KneeLeftPoint.Y)
            {
                if (j == false)
                {
                    lineNotify("警告！ 足首が膝よりも高いところにあります!");
                    notifyPicture("https://first-f4e63.web.app/ankle.jpg");
                }
                j = true;
            }
            else if (AnkleLeft.Y < KneeRightPoint.Y || AnkleRight.Y < KneeLeftPoint.Y)
            {
                j = false;
            }
			
			
			
			//// 肘が肩より高いところにある場合
            if (ElbowLeftPoint.Y > headPoint.Y || ElbowRightPoint.Y > headPoint.Y)
            {

                if (i == false)
                {
                    lineNotify("警告！ 肘が肩より高いところにあります!");
                    notifyPicture("https://first-f4e63.web.app/elbow.jpg");
                }
                i = true;

            }
            else if (ElbowLeftPoint.Y < headPoint.Y || ElbowRightPoint.Y < headPoint.Y)
            {
                i = false;
            }
        }

        
        /// SkeletonPointをレンダリング空間内にマッピングし、Pointに変換します。 
        
		/// <param name="skelpoint">ポイント→マップ</param>
        /// <returns>マップされたポイント</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // 点を深度空間に変換します。  
            // 奥行きを直接使うわけではありませんが、640x480の出力解像度でポイントが欲しいのです。
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        
        /// 2つの関節の間にボーンラインを描く
        
		
		/// <param name="skeleton">骨を描くための骨組み</param>
        /// <param name="drawingContext">描画先のコンテキスト</param>
        /// <param name="jointType0">描画を開始するジョイント</param>
        /// <param name="jointType1">で描画を終了するジョイント</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {


            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // これらのジョイントが見つからない場合は、終了します。
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // 両方のポイントが推測される場合は描かないでください
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // BOTHジョイントが追跡されない限り、描かれたボーンはすべて推定されると仮定します。
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }

      
        /// 着席モードのコンボボックスのチェック／アンチェックを処理する
      
        /// <param name="sender">イベントを送信するオブジェクト</param>
        /// <param name="e">イベント引数</param>
        private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                if (this.checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
        }


        /// </summary>
        /// --------  Line Notify Message --- ------------- 
		
		
		
		///  画像を送る
        private void notifyPicture(string url)
        {
            _lineNotify(" ", 0, 0, url);
        }
		
		/// sticker イメージとテキスト転送
		
        private void notifySticker(int stickerID, int stickerPackageID)
        {
            _lineNotify(" ", stickerPackageID, stickerID, "");
        }
		
		
		/// テキスト転送
		
        private void lineNotify(string msg)
        {
            _lineNotify(msg, 0, 0, "");
        }
		
		
		
		
        private void _lineNotify(string msg, int stickerPackageID, int stickerID, string pictureUrl)
        {
			
			/// ラインアカウントトークン情報
			
            string token = "PFDpATkd9EXZYLunRckxg47L7SlLwkJJkOoF4B8Fv6L";
			
			
            try
            {
				 // Line notify APIにリクエスト HttpWebRequest方式で
				
                var request = (HttpWebRequest)WebRequest.Create("https://notify-api.line.me/api/notify");


				// リクエストするデータ
				
                var postData = string.Format("message={0}", msg);
				
				// 要求するデータがsticker画像であるかどうかを判定
                
				if (stickerPackageID > 0 && stickerID > 0)
                {
                    var stickerPackageId = string.Format("stickerPackageId={0}", stickerPackageID);
                    var stickerId = string.Format("stickerId={0}", stickerID);
                    postData += "&" + stickerPackageId.ToString() + "&" + stickerId.ToString();
                }
				
				// 要求するデータがイメージであるかどうかを判断する
				
                if (pictureUrl != "")
                {
                    var imageThumbnail = string.Format("imageThumbnail={0}", pictureUrl);
                    var imageFullsize = string.Format("imageFullsize={0}", pictureUrl);
                    postData += "&" + imageThumbnail.ToString() + "&" + imageFullsize.ToString();
                }
				
				 // データをUTF-8方式に変換します。
				 
                var data = Encoding.UTF8.GetBytes(postData);


				//リクエスト方式はPOST方式です。
                request.Method = "POST";
				
				//頭部規約はx-www-form-urlencoded
				//UTF-8系列の文字を転送するための方式である。
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = data.Length;
                request.Headers.Add("Authorization", "Bearer " + token);

				/// streamオブジェクトに文字列を書く
                using (var stream = request.GetRequestStream()) stream.Write(data, 0, data.Length);
                var response = (HttpWebResponse)request.GetResponse();
                var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
            }
            catch (Exception ex)
            {
				
				//エラーメッセージ 
                Console.WriteLine(ex.ToString());
            }
        }

       


    }



}