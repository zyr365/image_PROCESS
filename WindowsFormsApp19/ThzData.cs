
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.UI;
using Emgu.Util;
using System.Drawing;
using System.Threading.Tasks;

using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WindowsFormsApp19
{
    public class THZData
    {
        private FrameMsg frameMsg;
        private Byte[] frameData;
        private int frameLength;
        private int ImageHeight;
        private int oriImageWidth;

        public bool isPeople;                   //人物判断
        public bool isHidden;
        private Matrix<int> backgroundframe;    //判定中间背景帧
        private List<Matrix<double>> multiImageList;//多帧平均列表
        public Matrix<double> PreProcessImage;  //预处理后图像
        public Matrix<byte> AlignImage;         //校准后图像
        public Matrix<int> MeanMat;             //输入的背景均值矩阵
        public Matrix<byte> FilterImage;        //滤波后图像
        public Mat FinalImage;                  //最终图像
        //public Dictionary<Rectangle, string> dictRects;//可疑块目标框架
        //public Dictionary<Mat, Rectangle> dictCoutours;//可疑轮廓内部信息
        public List<Rectangle> listRects;
        public bool MirrirFlag;
        public static bool Recorrect = false;
        int count = 0;

        const int JBianNum = 13;
        const int iHumanThreshold = 35;
        const int resizeImageWidth = 183;


        public THZData()
        {
            this.frameMsg = new FrameMsg();
            this.isPeople = false;
            this.isHidden = false;
            this.multiImageList = new List<Matrix<double>>();

        }

        public void Init(string allMsg)
        {
            if (allMsg.Count() > 0 && frameMsg != null)
            {
                //this.frameMsg.Init(allMsg.Substring(8, 36 * 2));
                this.frameMsg.Init_v1(allMsg.Substring(8, 36 * 2));
                this.ImageHeight = Convert.ToInt32(frameMsg.SampNum, 16); //BitConverter.ToInt32(Encoding.Default.GetBytes(frameMsg.SampNum), 0);
                this.oriImageWidth = Convert.ToInt32(frameMsg.ChannelNum, 16);//16进制str转int
                this.frameLength = Convert.ToInt32(frameMsg.DataLength, 16);// BitConverter.ToInt32(Encoding.Default.GetBytes(frameMsg.DataLength), 0);
                this.frameData = strToToHexByte(allMsg.Substring(80, frameLength * 2));//frameLength，28860

                if (MeanMat == null)
                    this.MeanMat = new Matrix<int>(new Size(1, ImageHeight * oriImageWidth));

                this.backgroundframe = new Matrix<int>(new Size(1, ImageHeight * oriImageWidth));
                this.PreProcessImage = new Matrix<double>(new Size(1, (ImageHeight - JBianNum) * oriImageWidth));
                this.AlignImage = new Matrix<byte>(new Size(resizeImageWidth * 2, (ImageHeight - JBianNum) * 2));
                this.FinalImage = new Mat();
                this.FilterImage = new Matrix<byte>(new Size(resizeImageWidth * 2, (ImageHeight - JBianNum) * 2));
                //this.dictRects = new Dictionary<Rectangle, string>();
                //this.dictCoutours = new Dictionary<Mat, Rectangle>();
                this.listRects = new List<Rectangle>();

            }
          

            //objForm.label1.Text = "123456789";
        }

        public void Init_v2(Form1 objForm)
        {
           // objForm.label1.Text = "hello";     //这样就可以找到我们定义的属性

        }

        /// <summary>
        /// 开始处理数据
        /// </summary>
        /// <param name="frame">PartData</param>
        public void StartImageProcess(Form1 objForm)
        {
            if (this.frameData == null)
                return;

            this.isHidden = false;
            //获取畸变矫正后初始矩阵
            getInitMatrix(objForm);

            //加权归一化
            Normalization(objForm);

            if (isPeople)
            {
                //滤波，分割，识别
                Filter(objForm);
                Seg_Reg(objForm);
            }
            else
            {
                CvInvoke.CvtColor(AlignImage, this.FinalImage, ColorConversion.Gray2Bgr);//Outputtemp,BoxArray
            }
            //CvInvoke.CvtColor(AlignImage, this.FinalImage, ColorConversion.Gray2Bgr);//Outputtemp,BoxArray

           
        }

        /// <summary>
        /// 获取畸变矫正后初始矩阵
        /// </summary>
        /// <param name="Data">Matrix<Byte></param>
        private void getInitMatrix(Form1 objForm)
        {

            Matrix<Byte> Data = new Matrix<Byte>(this.frameData);    //Byte转Matrix
            //临时变量temp1，temp2和temp3
            int Height = this.ImageHeight;//465
            int Width = this.oriImageWidth;//36
            Matrix<int> temp1 = new Matrix<int>(new Size(1, Height * Width));
            Matrix<double> temp2 = new Matrix<double>(new Size(1, Height * Width));
            //Matrix<double> temp3 = new Matrix<double>(new Size(1, Height * Width));

            //获取矩阵，temp1
            for (int i = 0; i < Height; i++)
            {
                for (int j = 0; j < Width; j++)
                {
                    //temp[i * n + j, 0] = (256 * msImg[i * 2 * (n + 1) + 2*j, 0] + msImg[i * 2 * (n + 1) + 2*j + 1, 0])/16;
                    temp1[i + j * Height, 0] = (256 * Data[i * 2 * (Width + 1) + 2 * j, 0] + Data[i * 2 * (Width + 1) + 2 * j + 1, 0]);
                }
            }

            //判断是否有人
            double MMMAX = 0;
            for (int j = 0; j < Width; j++)
            {
                Matrix<int> temptemp = new Matrix<int>(new Size(1, Height));
                for (int i = 0; i < Height; i++)
                {
                    temptemp[i, 0] = temp1[i + j * Height, 0];
                }
                //屏蔽某列
                //if (j == 35)
                //    continue;
                double Maxnum, Minnum;
                Point a, b;
                temptemp.MinMax(out Minnum, out Maxnum, out a, out b);

                MMMAX = (Maxnum - Minnum) > MMMAX ? (Maxnum - Minnum) : MMMAX;
            }
            isPeople = MMMAX > iHumanThreshold;                             //MMMax 行峰峰值
           




            //奇偶翻转，temp2
            if (this.frameMsg.Isodd)
            {
                for (int i = 0; i < Height; i++)
                {
                    for (int j = 0; j < Width; j++)
                    {
                        backgroundframe[j * Height + (Height - 1 - i), 0] = temp1[j * Height + i, 0];
                    }
                }
            }
            else
            {
                backgroundframe = temp1;
            }

            //背景校准
            if (!isPeople)
            {
                //自动背景校准
                MeanMat = MeanMat * 3 / 4 + backgroundframe / 4;
            }
            else if (Recorrect)
            {
                count++;
                //手动背景校准
                MeanMat = MeanMat * 3 / 4 + backgroundframe / 4;
                if (count > 15)
                    Recorrect = true;
            }



            //减背景均值，temp3
            //修改人 於景瞵 2018.6.1 列均值
            Matrix<double> MeanRow = new Matrix<double>(new Size(1, Width));
            for (int i = 0; i < Width; i++)
            {
                for (int j = 0; j < Height; j++)
                {
                    MeanRow[i, 0] += MeanMat[j + i * Height, 0];

                }
                MeanRow[i, 0] = MeanRow[i, 0] / Height;
            }
            for (int i = 0; i < Height; i++)
            {
                for (int j = 0; j < Width; j++)
                {
                    temp2[i + j * Height, 0] = (backgroundframe[i + j * Height, 0] - MeanRow[j, 0]); // 通道均匀系数* Mean_p[j, 0];
                    //屏蔽某列
                    //if (j == 35) //j == 33 || j == 34 ||
                    //    temp2[i + j * Height, 0] = 0;
                }
            }
            //修改人 於景瞵 2018.6.1 点均值
            //for (int i = 0; i < Height; i++)
            //{
            //    for (int j = 0; j < Width; j++)
            //    {
            //        temp2[i + j * Height, 0] = (backgroundframe[i + j * Height, 0] - MeanMat[i + j * Height, 0]) * Mean_p[j, 0];
            //        //屏蔽某列
            //        //if (j == 35) //j == 33 || j == 34 ||
            //        //    temp2[i + j * Height, 0] = 0;
            //    }
            //}

            //奇偶抖动矫正
            //if (this.frameMsg.Isodd)
            //{
            //    for (int i = 0; i < Height; i++)
            //    {
            //        for (int j = 0; j < Width; j++)
            //        {
            //            temp3[j * (Height) + i, 0] = temp2[j * Height + i, 0];
            //        }
            //    }
            //}
            //else
            //{
            //    for (int i = 0; i < Height ; i++)
            //    {
            //        for (int j = 0; j < Width; j++)
            //        {
            //            temp3[j * (Height) + i, 0] = temp2[j * Height + i, 0];
            //        }
            //    }
            //}

            //畸变矫正InitMatrix


            for (int i = 0; i < (Height) - JBianNum; i++)
            {
                for (int j = 0; j < Width / 2; j++)
                {
                    //PreProcessImage = InitMatrix;
                    /* if (MirrirFlag)
                     {
                         PreProcessImage[(2 * j) * (Height - JBianNum) + i, 0] = temp2[(2 * j) * (Height) + (i + 0), 0];
                         PreProcessImage[(2 * j + 1) * (Height - JBianNum) + i, 0] = temp2[(2 * j + 1) * (Height) + (i + JBianNum), 0];
                     }
                     //镜像
                     else
                     {
                         PreProcessImage[(Width - 2 * j - 1) * (Height - JBianNum) + i, 0] = temp2[(2 * j) * (Height) + (i + 0), 0];
                         PreProcessImage[(Width - 2 * j - 2) * (Height - JBianNum) + i, 0] = temp2[(2 * j + 1) * (Height) + (i + JBianNum), 0];
                     }*/
                    PreProcessImage[(Width - 2 * j - 1) * (Height - JBianNum) + i, 0] = temp2[(2 * j) * (Height) + (i + 0), 0];
                    PreProcessImage[(Width - 2 * j - 2) * (Height - JBianNum) + i, 0] = temp2[(2 * j + 1) * (Height) + (i + JBianNum), 0];
                }
            }

            objForm.imageBox1.Image = PreProcessImage.Mat;
            objForm.label1.Text = "getInitMatrix_PreProcessImage";

           


        }


        //[DllImport(@"C:/Users/lin/Desktop/dll/THZ_img_preprocess_dll_old.dll")]
       // public extern static IntPtr apply(byte[] src_data, int height_in, int width_in, int step_in, out int height_out, out int width_out, out int step_out, int enhance_mode, int img_height, int img_width, bool imshow_switch);
        /// <summary>
        /// 加权归一化,和插值
        /// </summary>
        private void Normalization(Form1 objForm)
        {
            int height = this.ImageHeight - JBianNum;     //矫正后图像高度
            int width = this.oriImageWidth;                          //可以屏蔽一些列


            Matrix<double> temp = new Matrix<double>(new Size(1, width * height));
            Matrix<Byte> temp2 = new Matrix<Byte>(new Size(width, height));
            //Matrix<Byte> temp3 = new Matrix<Byte>(new Size(183 * 2, height * 2));

            //加多帧平均算法
            multiImageList.Add(PreProcessImage);
            //加权系数
            double p1 = 0.0;
            double p2 = 0.0;
            double p3 = 1 - p1 - p2;
            if (multiImageList != null && multiImageList.Count > 3)
            {
                multiImageList.RemoveAt(0);
                temp = p1 * multiImageList[0] + p2 * multiImageList[1] + p3 * multiImageList[2];
            }
            else
            {
                temp = PreProcessImage;
            }

            //最大值最小值
            double Maxnum, Minnum;
            Point a, b;
            temp.MinMax(out Minnum, out Maxnum, out a, out b);
            //isPeople = (Maxnum - Minnum) > 70;

            //归一化
            if (isPeople)
            {
                //Parallel.For(0, height, i =>
                //{
                //    for (int j = 0; j < width; j++)
                //    {
                //        //temp2[i, j] = (Byte)(255 - (int)(Math.Pow(((temp[i + j * height, 0] - Minnum) / (Maxnum - Minnum)), 0.75) * 255));
                //        //temp2[i, j] = (Byte)(int)(Math.Pow(((temp[i + j * height, 0] - Minnum) / (Maxnum - Minnum)), 1.2) * 255);
                //        //镜像
                //        temp2[i, width - 1 - j] = (Byte)(int)(Math.Pow(((temp[i + j * height, 0] - Minnum) / (Maxnum - Minnum)), 1.2) * 255);
                //        //CvInvoke.Normalize(temp2,temp2,255);
                //    }
                //});

                for (int i = 0; i < height; i++)
                {
                    for (int j = 0; j < width; j++)
                    {
                        //temp2[i, j] = (Byte)(255 - (int)(Math.Pow(((temp[i + j * height, 0] - Minnum) / (Maxnum - Minnum)), 0.75) * 255));
                        //temp2[i, j] = (Byte)(int)(Math.Pow(((temp[i + j * height, 0] - Minnum) / (Maxnum - Minnum)), 1.2) * 255);
                        //镜像
                        //temp2[i, width - 1 - j] = (Byte)(int)(Math.Pow(((temp[i + j * height, 0] - Minnum) / (Maxnum - Minnum)), 1.2) * 255);
                        temp2[i, width - 1 - j] = (Byte)(int)((temp[i + j * height, 0] - Minnum) / (Maxnum - Minnum) * 255);

                    }
                }
                //CvInvoke.Normalize(temp, temp2,255);
            }
            else
            {
                //Parallel.For(0, height, i =>
                //{
                //    for (int j = 0; j < width; j++)
                //    {
                //        //temp2[i, j] = (Byte)(50 - (int)(Math.Pow(((temp[i + j * height, 0] - Minnum) / (Maxnum - Minnum)), 0.75) * 50));
                //        //temp2[i, j] = (Byte)(int)(Math.Pow(((temp[i + j * height, 0] - Minnum) / (Maxnum - Minnum)), 1.0) * 30);
                //        //镜像
                //        //temp2[i, width - 1 - j] = (Byte)(int)(Math.Pow(((temp[i + j * height, 0] - Minnum) / (Maxnum - Minnum)), 1.0) * 30);
                //        CvInvoke.Normalize(temp2, temp2, 30);
                //    }
                //});

                for (int i = 0; i < height; i++)
                {
                    for (int j = 0; j < width; j++)
                    {
                        //temp2[i, j] = (Byte)(50 - (int)(Math.Pow(((temp[i + j * height, 0] - Minnum) / (Maxnum - Minnum)), 0.75) * 50));
                        //temp2[i, j] = (Byte)(int)(Math.Pow(((temp[i + j * height, 0] - Minnum) / (Maxnum - Minnum)), 1.0) * 30);
                        //镜像
                        //temp2[i, width - 1 - j] = (Byte)(int)(Math.Pow(((temp[i + j * height, 0] - Minnum) / (Maxnum - Minnum)), 1.0) * 30);
                        temp2[i, width - 1 - j] = (Byte)(int)((temp[i + j * height, 0] - Minnum) / (Maxnum - Minnum) * 30);

                    }
                }
                //CvInvoke.Normalize(temp, temp2);


            }

           /* int height_out, width_out, step_out;
            Mat src_img_mat = new Mat();

            IntPtr dst_img_data = apply(temp2.Mat.GetData(), 465, 36, 36, out height_out, out width_out, out step_out, 2, 465, 216, false);
            src_img_mat = new Mat(height_out, width_out, 0, 1, dst_img_data, step_out);
            objForm.imageBox6.Image = src_img_mat;
            objForm.label6.Text = "lgy_iamge";*/

            //插值Resize
            CvInvoke.Resize(temp2, this.AlignImage, new Size(resizeImageWidth * 2, height * 2), 0, 0, Inter.Lanczos4);//183 * 2, height * 2
            objForm.imageBox2.Image = temp2.Mat;
            objForm.label2.Text = "Normalization_temp2";

            objForm.imageBox3.Image = AlignImage.Mat;
            objForm.label3.Text = "Normalization_AlignImage";


            //Form1 f1 = new Form1();
            // f1.showImage(AlignImage);
            // f1.label1.Text = "123456789";
            // f1.imageBox1.Image = AlignImage.Mat;
            //InitMiddleImage = temp3.Mat;
            //this.AlignImage = temp3;

        }

        /// <summary>
        /// filter滤波 
        /// </summary>
        private void Filter(Form1 objForm)
        {
            //isWarningImage = false;
            Matrix<byte> BoxArray = this.AlignImage.Clone();           //滤波矩阵
            //Mat MedianMat = InitMiddleImage.Clone();
            //Matrix<byte> MedianMat = this.AlignImage.Clone();
            //Mat Outputtemp = new Mat();
            //BoxArray._Mul(MedianMat);

            //中值滤波
            CvInvoke.MedianBlur(BoxArray, BoxArray, 9);
            //Boxfilter
            CvInvoke.BoxFilter(BoxArray, BoxArray, DepthType.Cv8U, new Size(9, 9), new Point(-1, -1), true, BorderType.Reflect101);
            //CvInvoke.FastNlMeansDenoising(MedianMat,BoxArray);

            //背景归0
            //Matrix<byte> tempArray = new Matrix<byte>(new Size(BoxArray.Width, BoxArray.Height));
            //CvInvoke.Threshold(BoxArray, tempArray, 100, 1, ThresholdType.Binary);
            //BoxArray._Mul(tempArray);

            //ACE图像增强
            BoxArray = ACE(BoxArray);
            //CvInvoke.MedianBlur(BoxArray, BoxArray, 9);
            CvInvoke.BoxFilter(BoxArray, BoxArray, DepthType.Cv8U, new Size(9, 9), new Point(-1, -1), true, BorderType.Reflect101);

            //输出图像为滤波后图像转三通道
            //CvInvoke.Threshold(BoxArray, Outputtemp, Th>50?Th:50, 255, ThresholdType.ToZero);
            CvInvoke.CvtColor(BoxArray, this.FinalImage, ColorConversion.Gray2Bgr);//Outputtemp,BoxArray
            this.FilterImage = BoxArray;

            //objForm.imageBox4.Image = FilterImage.Mat;
            objForm.imageBox4.Image = FinalImage;
            objForm.label4.Text = "Filter_FinalImage";

           // CvInvoke.Imshow("img2", FinalImage);
            //objForm.imageBox4.Dispose();



        }
        /// <summary>
        /// 自适应阈值分割 + 目标区域方块提取
        /// </summary>
        private void Seg_Reg(Form1 objForm)
        {
            ///分割
            //手动阈值
            double Th = graythresh(FilterImage.Mat.GetData());//* 1.10
            //阈值分割矩阵
            //Matrix<byte> ThreArray = InitMiddleImage.Clone();
            //自动阈值分割。。。自适应阈值分割，使用工具包
            //CvInvoke.Threshold(BoxArray, ThreArray, 80, 1, ThresholdType.Otsu);
            //BoxArray._Mul(ThreArray);

            //手动阈值分割。。。手动阈值分割，自写函数
            //double Th = graythresh(BoxArray.Mat.GetData())*1.10;
            CvInvoke.Threshold(FilterImage, FilterImage, Th * 1.10, 255, ThresholdType.ToZero);//InputArray,BoxArray,ThresholdType.ToZero,ThresholdType.Binary


            objForm.imageBox5.Image = FilterImage.Mat;
            objForm.label5.Text = "Seg_Reg_FilterImage";
            ///识别            
            regCoutour();

            ///画图
            drawCoutours();
        }

        bool matchCoutours(Mat roi)
        {
            //中心区域为黑
            if (CvInvoke.Mean(roi).V0 > 60)
                return false;
            else
                return true;
        }

        void drawCoutours()
        {
            var color = new MCvScalar(0, 0, 255);
            //if (isPeople)
            {

                //if (CountNum % 3 == 0)  //修改人 於景瞵 2018.4.23   
                {
                    //if (rects != null && rects.Count != 0 && rects.Count <= 10)
                    if (listRects != null && listRects.Count != 0 && listRects.Count <= 10) //修改时间18.4.16
                    {
                        ////isWarningImage = true;
                        ////HideGoods = "到" + dictRects.Count + "个危险品";
                        //foreach (var rect in dictRects)
                        //{
                        //    if (rect.Value == "一类")
                        //    {
                        //        HideGoods = "到" + rect.Value + "危险品";
                        //        break;
                        //    }
                        //    HideGoods = "到" + rect.Value + "危险品";
                        //}
                    }
                }

                if (listRects.Count <= 10 && listRects.Count > 0)
                {
                    this.isHidden = true;
                    foreach (var rect in listRects)
                    {
                        //Mat roi = new Mat(rect,new Rectangle(new Point(rect.Value.Width/2-1,rect.Value.Height/2-1),new Size(3,3)));
                        //中心区域为黑
                        //if (CvInvoke.Mean(roi).V0 > 60)
                        //    continue;
                        //if (matchCoutours(roi))
                        {
                            CvInvoke.Rectangle(FinalImage, rect, color);
                            //CvInvoke.PutText(FinalImage, "Hidden", rect.Value.Location, FontFace.HersheyComplex, 0.4, color);
                        }
                    }
                }
                //CountNum++; //修改人 於景瞵 2018.4.23 
            }

        }

        /// <summary>
        /// 轮廓提取函数
        /// 20180416 修改人 於景瞵 
        /// </summary>
        /// <param name="srcimage"> 原始图像</param>
        /// <param name="OutImage"> 存放轮廓图像</param>
        /// <returns> 返回目标图像</returns>
        private void regCoutour()
        {
            Mat srcimage = this.FilterImage.Mat.Clone();
            Mat hierarchy = new Mat();
            Emgu.CV.Util.VectorOfVectorOfPoint Coutours = new Emgu.CV.Util.VectorOfVectorOfPoint();
            CvInvoke.FindContours(srcimage, Coutours, hierarchy, RetrType.List, ChainApproxMethod.LinkRuns, new Point(0, 0));
            //imageBox1.Image = b2;

            //List<Rectangle> rects = new List<Rectangle>();
            //开始遍历
            for (int i = 0; i < Coutours.Size; i++)
            {
                //得到这个连通区域的外接矩形
                var rect = CvInvoke.BoundingRectangle(Coutours[i]);
                //var Rect = CvInvoke.MinAreaRect(Coutours[i]);
                //var Area = CvInvoke.ContourArea(Coutours[i]);
                //如果高度不足，或者长宽比太小，认为是无效数据，否则把矩形画到原图上
                if ((rect.Height > 13 && rect.Width > 13) && (rect.Height < srcimage.Rows / 4 || rect.Width < srcimage.Cols / 4)
                    && rect.Height < srcimage.Rows * 3 / 5 && rect.Left > srcimage.Cols / 6 && rect.Right < srcimage.Cols * 5 / 6
                    && rect.Top > srcimage.Rows / 4 && rect.Bottom < srcimage.Rows * 3 / 4)
                {

                    //int a = srcimage.Bitmap.GetPixel(rect.X + rect.Width / 2, rect.Y + rect.Height / 2).B;
                    var Rect = CvInvoke.MinAreaRect(Coutours[i]);
                    var Area = CvInvoke.ContourArea(Coutours[i]);
                    listRects.Add(rect);
                    //if (Area / (Rect.Size.Height * Rect.Size.Width) < 0.8)
                    //    dictRects.Add(rect, "一类");
                    //else if (Rect.Size.Height / Rect.Size.Width < 1.0 / 3.0 || Rect.Size.Height / Rect.Size.Width > 3.0)
                    //    dictRects.Add(rect, "一类");
                    //else if (Area > 2000)
                    //    dictRects.Add(rect, "一类");
                    //else
                    //    dictRects.Add(rect, "二类");
                    //CvInvoke.DrawContours(b1, b3, i, color);
                }
            }

            //foreach (var rect in listRects)
            //{
            //    Mat roiMat = new Mat(FilterImage.Mat,rect.Key);
            //    this.dictCoutours.Add(roiMat, rect.Key);
            //}
        }

        /// <summary>
        /// 20180323 修改人 於景瞵 
        /// ACE图像增强算法
        /// </summary>
        /// <param name="src"></param>
        /// <param name="MaxCG"></param>
        /// <param name="C"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        Matrix<byte> ACE(Matrix<byte> src, double MaxCG = 2.0, int Amount = 140, int Radius = 40)
        {
            //图像高宽
            int rows = src.Width;
            int cols = src.Height;

            //背景归0化
            //Matrix<byte> ThreArray = new Matrix<byte>(new Size(rows, cols));
            //CvInvoke.Threshold(src, ThreArray, 60, 1, ThresholdType.Otsu);
            //src._Mul(ThreArray);

            //源图像double初始化
            Matrix<double> Src = src.Convert<double>() / 255.0;
            //全局和局部均值标准差矩阵初始化
            Matrix<double> meanLocal = new Matrix<double>(new Size(rows, cols)); //图像局部均值  
            Matrix<double> varLocal = new Matrix<double>(new Size(rows, cols));  //图像局部方差  
            Matrix<double> meanGlobal = new Matrix<double>(new Size(rows, cols));//全局均值
            Matrix<double> varGlobal = new Matrix<double>(new Size(rows, cols)); //全局标准差  

            //局部均值和高频成分计算
            CvInvoke.Blur(Src, meanLocal, new Size(Radius, Radius), new Point(-1, -1));
            Matrix<double> highFreq = Src - meanLocal;//高频成分 

            //局部标准差计算
            varLocal = highFreq.Clone();
            varLocal._Mul(highFreq);
            CvInvoke.Blur(varLocal, varLocal, new Size(Radius, Radius), new Point(-1, -1));
            CvInvoke.Sqrt(varLocal, varLocal);

            //CGArr初始化
            Matrix<double> cGArr = new Matrix<double>(new Size(rows, cols));

            //全局均值与标准差计算
            MCvScalar meanGloble = new MCvScalar();
            MCvScalar stdGloble = new MCvScalar();
            CvInvoke.MeanStdDev(Src, ref meanGloble, ref stdGloble);

            //求取CGArr
            double D = Amount * stdGloble.V0 / 100;                             //CGArr系数
            Matrix<double> temp = new Matrix<double>(new Size(rows, cols)) + 1;
            CvInvoke.Divide(D * temp, varLocal, cGArr);
            //cGArr = Amount * varLocal / stdGloble.V0;

            //封顶CGArr
            Matrix<byte> tempArr = cGArr.Convert<byte>();                       //cgArr转byte
            Matrix<byte> hFArr = tempArr.Clone();                               //封顶高频系数
            CvInvoke.Threshold(tempArr, hFArr, MaxCG, 1, ThresholdType.Binary);
            cGArr._Mul(1.0 - hFArr.Convert<double>());
            cGArr = (MaxCG * hFArr.Convert<double>()) + cGArr;
            cGArr._Mul(highFreq);

            //返回值Byte化
            Matrix<byte> dst1 = ((meanLocal + cGArr) * 255).Convert<byte>();     //变增益方法
            //Mat dst2 = (meanLocal + Amount * highFreq).Convert<byte>().Mat;//恒增益方法

            return dst1;
        }

        #region graythresh灰度阈值子函数
        private static double[] getHist(byte[] img1)
        {
            int gray = 0;
            //int Width = img.GetLength(1);
            //int Height = img.GetLength(0);
            int length = img1.Length;
            double[] Hist = new double[256];
            double[] p = new double[256];
            double dSumHist = 0.0;

            Parallel.For(0, length, i =>
            {

                gray = img1[i];
                Hist[gray] += 1.0;

            });

            //不统计0项
            //Hist[0] = 0;

            dSumHist = Hist.Sum();

            for (int i = 0; i < 256; i++)
            {
                p[i] = Hist[i] / dSumHist;

            }

            return p;
        }

        private static int iGetMaxidFromSigma_b_squared(double[] p)
        {
            int iMaxid = 0;
            int Length = p.GetLength(0);
            double[] dOmege = new double[Length];
            double[] dMu = new double[Length];
            double dMuMax = 0.0;
            double[] dSigma_b_squared = new double[Length];
            double dMaxSigma = 0.0;

            for (int i = 0; i < Length; i++)
            {
                if (i == 0)
                    dOmege[i] = p[i];
                else
                    dOmege[i] = dOmege[i - 1] + p[i];
            }//get Omege


            for (int i = 0; i < Length; i++)
            {
                if (i == 0)
                    dMu[i] = p[i];
                else
                    dMu[i] = dMu[i - 1] + p[i] * (i + 1);
            }//get Mu
            dMuMax = dMu[Length - 1];//get MuMax


            for (int i = 0; i < Length; i++)
            {
                dSigma_b_squared[i] = (dMuMax * dOmege[i] - dMu[i]) * (dMuMax * dOmege[i] - dMu[i]) / (dOmege[i] * (1 - dOmege[i]));
                if (dMaxSigma < dSigma_b_squared[i])
                {
                    dMaxSigma = dSigma_b_squared[i];
                    iMaxid = i;
                }

            }//get dSigma_b_squared

            return iMaxid;
        }


        private static double graythresh(byte[] Input1)
        {
            double Th = new double();
            //int Length = Input.GetLength(0);
            //int Width = Input.GetLength(1);

            double[] p = getHist(Input1);
            Th = iGetMaxidFromSigma_b_squared(p);


            return Th;
        }
        #endregion

        /// <summary>
        /// 字符串转16进制Byte字节
        /// </summary>
        /// <param name="hexString">输入字符串</param>
        /// <returns>转化的Byte字节</returns>
        private byte[] strToToHexByte(string hexString)
        {
            hexString = hexString.Replace("-", "");
            if ((hexString.Length % 2) != 0)
                hexString += "20";
            byte[] returnBytes = new byte[hexString.Length / 2];
            for (int i = 0; i < returnBytes.Length; i++)
                returnBytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            return returnBytes;
        }

        public double[] GetTemparture()
        {
            double[] Temparture = new double[4];
            if (this.frameMsg.TempData == null)
                return Temparture;
            byte[] Wen = strToToHexByte(this.frameMsg.TempData);
            if ((Wen[0] & 0xf0) >> 4 == 15)
                Temparture[0] = -(~Wen[1] + 1 + 256.0 * ~Wen[0]) / 16;
            else
                Temparture[0] = (Wen[1] + 256.0 * Wen[0]) / 16;

            if ((Wen[2] & 0xf0) >> 4 == 15)
                Temparture[1] = -(~Wen[3] + 1 + 256.0 * ~Wen[2]) / 16;
            else
                Temparture[1] = (Wen[3] + 256.0 * Wen[2]) / 16;

            if ((Wen[4] & 0xf0) >> 4 == 15)
                Temparture[2] = -(~Wen[5] + 1 + 256.0 * ~Wen[4]) / 16;
            else
                Temparture[2] = (Wen[5] + 256.0 * Wen[4]) / 16;

            if ((Wen[6] & 0xf0) >> 4 == 15)
                Temparture[3] = -(~Wen[7] + 1 + 256.0 * ~Wen[6]) / 16;
            else
                Temparture[3] = (Wen[7] + 256.0 * Wen[6]) / 16;
            return Temparture;
        }
    }

    public class FrameMsg
    {
        //public string Head;
        public string HeadLength;
        public string TairLength;
        public string DataLength;
        public string ImageNum;
        public string ChannelNum;
        public string SampNum;
        public string EncodedData;
        public string TempData;
        public string DigSmall;
        public string DianjiState;
        public string VerDefe;
        public string HorDefe;
        public string Kuozhan;
        public string Tair;
        public bool Isodd;

        public void Init(string msg)
        {
            this.HeadLength = msg.Substring(0, 2 * 2);
            this.TairLength = msg.Substring(2 * 2, 2 * 2);
            this.DataLength = msg.Substring(4 * 2, 4 * 2);
            this.ImageNum = msg.Substring(8 * 2, 4 * 2);
            this.ChannelNum = msg.Substring(12 * 2, 2 * 2);
            this.SampNum = msg.Substring(14 * 2, 2 * 2);
            this.EncodedData = msg.Substring(16 * 2, 4 * 2);
            this.TempData = msg.Substring(20 * 2, 8 * 2);
            this.DigSmall = msg.Substring(28 * 2, 1 * 2);
            this.DianjiState = msg.Substring(29 * 2, 1 * 2);
            this.Kuozhan = msg.Substring(30 * 2, 6 * 2);

            this.Isodd = ((Convert.ToInt32(this.ImageNum, 16) & 0x01) == 1);
        }

        public void Init_v1(string msg)
        {
            this.HeadLength = msg.Substring(0, 2 * 2);
            this.TairLength = msg.Substring(2 * 2, 2 * 2);
            this.DataLength = msg.Substring(4 * 2, 4 * 2);
            this.ImageNum = msg.Substring(8 * 2, 4 * 2);
            this.ChannelNum = msg.Substring(12 * 2, 2 * 2);
            this.SampNum = msg.Substring(14 * 2, 2 * 2);
            //this.EncodedData = msg.Substring(16 * 2, 4 * 2);
            this.TempData = msg.Substring(16 * 2, 8 * 2);
            this.DigSmall = msg.Substring(24 * 2, 1 * 2);
            this.DianjiState = msg.Substring(25 * 2, 1 * 2);
            this.VerDefe = msg.Substring(26 * 2, 2 * 2);
            this.HorDefe = msg.Substring(28 * 2, 2 * 2);
            this.Kuozhan = msg.Substring(30 * 2, 6 * 2);

            this.Isodd = ((Convert.ToInt32(this.ImageNum, 16) & 0x01) == 1);
        }
    }
}
