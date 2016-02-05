using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

namespace CEERecognition
{
    public class PointData
    {
        public float[] m_Points;

        public PointData(PointData refShape)
        {
            m_Points = new float[refShape.m_Points.Length];
            for (int i = 0; i < m_Points.Length; i++)
                m_Points[i] = refShape.m_Points[i];
        }
        public PointData(int numPoints)
        {
            m_Points = new float[numPoints * 2];
            for (int i = 0; i < m_Points.Length; i++)
                m_Points[i] = 0;
        }
        public PointData(float[] points, int num)
        {
            m_Points = new float[num * 2];
            for (int i = 0; i < m_Points.Length; i++)
                m_Points[i] = points[i];
        }

        public int PointNum() { return m_Points.Length / 2; }
        public float X(int index) { return m_Points[index * 2]; }
        public float Y(int index) { return m_Points[index * 2 + 1]; }
        public void SetX(int index, float value) { m_Points[index * 2] = value; }
        public void SetY(int index, float value) { m_Points[index * 2 + 1] = value; }
        public float[] Data() { return m_Points; }

        float Norm2()
        {
            double norm = 0;
            for (int i = 0; i < m_Points.Length; i++)
                norm += m_Points[i] * m_Points[i];
            return (float)Math.Sqrt(norm);
        }

        public void Normalize()
        {
            float centerx, centery, norm;
            CenterPoint(out centerx, out centery);
            Translate(-centerx, -centery);
            norm = Norm2();
            Scale(1 / norm);
        }

        // geometric transformation
        void Scale(float s)
        {
            for (int i = 0; i < m_Points.Length; i++)
                m_Points[i] *= s;
        }

        void Translate(float x, float y)
        {
            for (int i = 0; i < PointNum(); i++)
            {
                m_Points[i * 2] += x;
                m_Points[i * 2 + 1] += y;
            }
        }

        public void CenterPoint(out float x, out float y)
        {
            int numPoints = PointNum();
            x = 0; y = 0;
            if (m_Points.Length == 0) return;
            for (int i = 0; i < numPoints; i++)
            {
                x += m_Points[i * 2];
                y += m_Points[i * 2 + 1];
            }
            x /= numPoints;
            y /= numPoints;
        }

        // See Ref: Cootes, Timothy F., et al. "Active shape models-their training and application." Computer vision and image understanding 61.1 (1995): 38-59.
        // http://personalpages.manchester.ac.uk/staff/timothy.f.cootes/Papers/cootes_cviu95.pdf
        // Note that we centralize each of the two shapes, making X_i and Y_i in Eq. 31 be zero. This trick makes the linear equation Eq. 30 easy to solve.
        public void AlignNorm2(PointData refShape)
        {
            PointData refCopy = new PointData(refShape);

            // Translate both shapecenter to (0,0) 
            float refCenterx, refCentery;
            refCopy.CenterPoint(out refCenterx, out refCentery);
            refCopy.Translate(-refCenterx, -refCentery);
            float centerx, centery;
            CenterPoint(out centerx, out centery);
            Translate(-centerx, -centery);

            // (x,y) is aligned to refshape (x',y')
            // SXX1 = E(x*x') SYY1 = E(y*y') SXY1 = E(x*y') SYX1 = E(y*x')
            // SXX = E(x*x) SYY = E(y*y)
            // x' = ax -by +tx
            // y' = bx +ay +ty
            // a = (SXX1+SYY1)/(SXX+SYY)
            // b = (SXY1-SYX1)/(SXX+SYY)
            if (0 == PointNum()) return;
            float SXX, SYY, SXX1, SYY1, SXY1, SYX1;
            SXX = SYY = SXX1 = SYY1 = SXY1 = SYX1 = 0;
            for (int i = 0; i < PointNum(); i++)
            {
                float x, y, x1, y1;
                x = X(i); y = Y(i);
                x1 = refCopy.X(i); y1 = refCopy.Y(i);
                SXX += x * x; SYY += y * y;
                SXX1 += x * x1; SYY1 += y * y1;
                SXY1 += x * y1; SYX1 += y * x1;
            }
            float a, b;
            if (SXX + SYY == 0) return;
            a = (SXX1 + SYY1) / (SXX + SYY);
            b = (SXY1 - SYX1) / (SXX + SYY);

            // x' = ax -by +tx
            // y' = bx +ay +ty
            for (int i = 0; i < PointNum(); i++)
            {
                float x, y;
                x = X(i); y = Y(i);
                SetX(i, a * x - b * y);
                SetY(i, b * x + a * y);
            }
            Translate(refCenterx, refCentery);
        }

        public void AlignNorm2(PointData refShape, out float a, out float b)
        {
            PointData refCopy = new PointData(refShape);
            a = 1.0f;
            b = 0.0f;

            // Translate both shapecenter to (0,0) 
            float refCenterx, refCentery;
            refCopy.CenterPoint(out refCenterx, out refCentery);
            refCopy.Translate(-refCenterx, -refCentery);
            float centerx, centery;
            CenterPoint(out centerx, out centery);
            Translate(-centerx, -centery);

            // (x,y) is aligned to refshape (x',y')
            // SXX1 = E(x*x') SYY1 = E(y*y') SXY1 = E(x*y') SYX1 = E(y*x')
            // SXX = E(x*x) SYY = E(y*y)
            // x' = ax -by +tx
            // y' = bx +ay +ty
            // a = (SXX1+SYY1)/(SXX+SYY)
            // b = (SXY1-SYX1)/(SXX+SYY)
            if (0 == PointNum()) return;
            float SXX, SYY, SXX1, SYY1, SXY1, SYX1;
            SXX = SYY = SXX1 = SYY1 = SXY1 = SYX1 = 0;
            for (int i = 0; i < PointNum(); i++)
            {
                float x, y, x1, y1;
                x = X(i); y = Y(i);
                x1 = refCopy.X(i); y1 = refCopy.Y(i);
                SXX += x * x; SYY += y * y;
                SXX1 += x * x1; SYY1 += y * y1;
                SXY1 += x * y1; SYX1 += y * x1;
            }
            //float a, b;
            if (SXX + SYY == 0) return;
            a = (SXX1 + SYY1) / (SXX + SYY);
            b = (SXY1 - SYX1) / (SXX + SYY);

            // x' = ax -by +tx
            // y' = bx +ay +ty
            for (int i = 0; i < PointNum(); i++)
            {
                float x, y;
                x = X(i); y = Y(i);
                SetX(i, a * x - b * y);
                SetY(i, b * x + a * y);
            }
            Translate(refCenterx, refCentery);
        }

        public static float Distance(PointData s1, PointData s2)
        {
            float dist = 0;
            for (int i = 0; i < s1.m_Points.Length; i++)
            {
                dist += (s1.m_Points[i] - s2.m_Points[i]) * (s1.m_Points[i] - s2.m_Points[i]);
            }
            return dist;
        }
    }

    public class Helper
    {
        public static float[] BinaryToFloatArray(byte[] binary)
        {
            var r = new float[binary.Length / 4];
            Buffer.BlockCopy(binary, 0, r, 0, binary.Length);
            return r;
        }

        public static byte[] FloatArrayToBinary(float[] farray)
        {
            var r = new byte[farray.Length * sizeof(float)];
            Buffer.BlockCopy(farray, 0, r, 0, r.Length);
            return r;
        }
    }

    public class FaceProcess
    {
        private static int facialPointsNum = 27;
        private static float[] templatePoints = new float[54];

        public void LoadAlignmentTemplate()
        {
            //private double[] templatePoints_d = new double[27] {-0.1435745, -0.09751324, 0.1465635, -0.09545494, -0.002502798, 0.08669894, -0.134234, 0.2176705, 0.1327966, 0.2200314, -0.2597697, -0.1601567, -0.06860603, -0.1588396, -0.195908, -0.09441547, -0.1457139, -0.1164698, -0.1468758, -0.07554296, -0.09656556, -0.08993812, 0.06787405, -0.1587539, 0.2640095, -0.1565289, 0.09899407, -0.0884045, 0.1486475, -0.1143898, 0.1493653, -0.073224, 0.1989327, -0.09132996, -0.04037917, -0.08282474, 0.04079287, -0.08254138, -0.06442133, 0.03188311, 0.06242511, 0.03322322, -0.09343174, 0.09200199, 0.09143509, 0.09396633, -0.002061998, 0.1931304, -0.002255796, 0.2202828, -0.002566518, 0.2530539, -0.002969528, 0.2943854 };
            double[] templatePoints_d = new double[] { -0.1435745, -0.09751324, 0.1465635, -0.09545494, -0.002502798, 0.08669894, -0.134234, 0.2176705, 0.1327966, 0.2200314, -0.2597697, -0.1601567, -0.06860603, -0.1588396, -0.195908, -0.09441547, -0.1457139, -0.1164698, -0.1468758, -0.07554296, -0.09656556, -0.08993812, 0.06787405, -0.1587539, 0.2640095, -0.1565289, 0.09899407, -0.0884045, 0.1486475, -0.1143898, 0.1493653, -0.073224, 0.1989327, -0.09132996, -0.04037917, -0.08282474, 0.04079287, -0.08254138, -0.06442133, 0.03188311, 0.06242511, 0.03322322, -0.09343174, 0.09200199, 0.09143509, 0.09396633, -0.002061998, 0.1931304, -0.002255796, 0.2202828, -0.002566518, 0.2530539, -0.002969528, 0.2943854 };
            for (int x = 0; x < templatePoints_d.Length; x++)
            {
                templatePoints[x] = Convert.ToSingle(templatePoints_d[x].ToString());
            }
        }

        private static void facialPointsScale(float[] facialPoints, int imageWidth, int imageHeight, int facialPointsNum)
        {
            for (int i = 0; i < facialPointsNum; i++)
            {
                facialPoints[i * 2] = facialPoints[i * 2] * imageWidth;
                facialPoints[i * 2 + 1] = facialPoints[i * 2 + 1] * imageHeight;
            }
        }

        public static Bitmap RotateImgWithFaceCropping2(Bitmap bmp, Color bkColor, float facialCenterx, float facialCentery, float a, float b, float templateFacialWidth)
        {
            //float angle = (float)Math.Atan((bfy - afy) / (afx - bfx + 0.00000000000000000001));
            float angle = (float)Math.Atan(b / (a + 0.00000000000000000001));
            angle = angle % 360;
            if (angle > 180)
                angle -= 360;

            float shrinkage = (float)Math.Sqrt((double)(a * a + b * b));
            float cos = a / shrinkage;
            float sin = b / shrinkage;

            System.Drawing.Imaging.PixelFormat pf = default(System.Drawing.Imaging.PixelFormat);
            if (bkColor == Color.Transparent)
            {
                pf = System.Drawing.Imaging.PixelFormat.Format32bppArgb;
            }
            else
            {
                pf = bmp.PixelFormat;
            }

            // float sin = (float)Math.Abs(Math.Sin(angle)); // this function takes radians
            //  float cos = (float)Math.Abs(Math.Cos(angle)); // this one too


            float newImgWidth = (float)Math.Abs(sin) * bmp.Height + (float)Math.Abs(cos) * bmp.Width;
            float newImgHeight = (float)Math.Abs(sin) * bmp.Width + (float)Math.Abs(cos) * bmp.Height;

            float originX = 0f;
            float originY = 0f;
            //float afrx = 0f;
            //float afry = 0f;
            //float bfrx = 0f;
            //float bfry = 0f;

            if (sin > 0)
            {
                if (cos > 0)
                {
                    originX = sin * bmp.Height;
                    //afrx = (bmp.Height - afy) * sin + afx * cos;
                    //afry = (afy) * cos + afx * sin;
                    //bfrx = (bmp.Height - bfy) * sin + bfx * cos;
                    //bfry = (bfy) * cos + bfx * sin;
                }

                else
                {
                    originX = newImgWidth;
                    originY = newImgHeight - sin * bmp.Width;
                }
            }
            else
            {
                if (cos > 0)
                {
                    originY = -sin * bmp.Width;
                    //afrx = (afy) * sin + afx * cos;
                    //afry = (afy) * cos + (bmp.Width - afx) * sin;
                    //bfrx = (bfy) * sin + bfx * cos; ;
                    //bfry = (bfy) * cos + (bmp.Width - bfx) * sin;
                }
                else
                {
                    originX = newImgWidth + sin * bmp.Height;
                    originY = newImgHeight;
                }
            }

            //facialCenterx = facialCenterx*bmp.Width;
            //facialCentery = facialCentery*bmp.Height;
            float newfacialCenterx = facialCenterx * cos - facialCentery * sin + originX;
            float newfacialCentery = facialCenterx * sin + facialCentery * cos + originY;

            float fwe = templateFacialWidth / shrinkage;

            int leftupx = (int)(newfacialCenterx - 1.5 * fwe); //
            int leftupy = (int)(newfacialCentery - 1.75 * fwe); //Twice the size?!!
            int rightdownx = (int)(newfacialCenterx + 1.5 * fwe);
            int rightdowny = (int)(newfacialCentery + 1.75 * fwe);

            //int leftupx = (int)(afrx - 1.5 * fwe); //
            //int leftupy = (int)(afry - 2 * fwe); //Twice the size?!!
            //int rightdownx = (int)(bfrx + 1.5 * fwe);
            //int rightdowny = (int)(bfry + 2.5 * fwe);

            if (leftupx < 0)
                leftupx = 0;
            if (leftupy < 0)
                leftupy = 0;
            if (rightdownx >= (int)newImgWidth)
                rightdownx = (int)newImgWidth - 1;
            if (rightdowny >= (int)newImgHeight)
                rightdowny = (int)newImgHeight - 1;

            Bitmap newImg = new Bitmap((int)newImgWidth, (int)newImgHeight, pf);
            Graphics g = Graphics.FromImage(newImg);
            g.Clear(bkColor);
            g.TranslateTransform(originX, originY); // offset the origin to our calculated values
            g.RotateTransform((float)(angle * 180.0 / Math.PI)); // set up rotate
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
            //g.DrawImageUnscaled(bmp, 0, 0); // draw the image at 0, 0
            g.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height); // draw the image at 0, 0
            g.Dispose();

            Rectangle cropRectangle = new Rectangle(leftupx, leftupy, rightdownx - leftupx, rightdowny - leftupy);

            Bitmap faceImg = cropImage((Image)newImg, cropRectangle);
            return faceImg;
        }
        private static Bitmap cropImage(Image img, Rectangle cropArea)
        {
            Bitmap bmpImage = new Bitmap(img);
            return bmpImage.Clone(cropArea, bmpImage.PixelFormat);
        }
        //public void faceAlignedCropping(byte[] inputImage, byte[] croppedImage, byte[] facialPoints_b)
        public Bitmap faceAlignedCropping(Bitmap img, byte[] facialPoints_b)
        {
            float[] facialPoints = Helper.BinaryToFloatArray(facialPoints_b);

            //readFacialPoints("test", facialPointsNum);
            float facialWidth = Math.Abs(templatePoints[2] - templatePoints[0]);
            PointData templateP = new PointData(templatePoints, facialPointsNum);


            facialPointsScale(facialPoints, img.Width, img.Height, facialPointsNum);
            PointData facialP = new PointData(facialPoints, facialPointsNum);
            float facialCenterx, facialCentery;
            facialP.CenterPoint(out facialCenterx, out facialCentery);
            float a, b;
            facialP.AlignNorm2(templateP, out a, out b);

            //public static Image RotateImgWithFaceCropping2(Bitmap bmp, Color bkColor, float facialCenterx, float facialCentery, float a, float b)

            Bitmap faceImg = RotateImgWithFaceCropping2((Bitmap)(img), Color.Transparent, facialCenterx, facialCentery, a, b, facialWidth);
            return faceImg;
        }

        public byte[] faceAlignedCroppingToFixedSize(byte[] inputImage, byte[] facialPoints_b, int outputWidth, int outputHeight)
        {
            float[] facialPoints = Helper.BinaryToFloatArray(facialPoints_b);
            Stream jpegImageStream = new MemoryStream(inputImage);
            Image img = Image.FromStream(jpegImageStream);

            //readFacialPoints("test", facialPointsNum);
            float facialWidth = Math.Abs(templatePoints[2] - templatePoints[0]);
            PointData templateP = new PointData(templatePoints, facialPointsNum);


            facialPointsScale(facialPoints, img.Width, img.Height, facialPointsNum);
            PointData facialP = new PointData(facialPoints, facialPointsNum);
            float facialCenterx, facialCentery;
            facialP.CenterPoint(out facialCenterx, out facialCentery);
            float a, b;
            facialP.AlignNorm2(templateP, out a, out b);

            //public static Image RotateImgWithFaceCropping2(Bitmap bmp, Color bkColor, float facialCenterx, float facialCentery, float a, float b)
            Image faceImg = RotateImgWithFaceCropping2((Bitmap)(img), Color.Transparent, facialCenterx, facialCentery, a, b, facialWidth);
            MemoryStream jpegFaceImageStream = new MemoryStream();

            faceImg.Save(jpegFaceImageStream, ImageFormat.Bmp);
            byte[] croppedImage = jpegFaceImageStream.ToArray();
            return croppedImage;
        }

        public Bitmap ImageResize(Bitmap img, int outputWidth, int outputHeight)
        {
            System.Drawing.Imaging.PixelFormat pf = default(System.Drawing.Imaging.PixelFormat);
            //if (bkColor == Color.Transparent)
            //{
            pf = System.Drawing.Imaging.PixelFormat.Format32bppArgb;
            //}
            //else
            // {
            //     pf = bmp.PixelFormat;
            // }

            Bitmap newImg = new Bitmap((int)outputWidth, (int)outputHeight, pf);
            Graphics g = Graphics.FromImage(newImg);
            g.Clear(Color.Transparent);

            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
            //g.DrawImageUnscaled(bmp, 0, 0); // draw the image at 0, 0
            g.DrawImage(img, 0, 0, outputWidth, outputHeight); // draw the image at 0, 0
            g.Dispose();

            return newImg;
        }

        public byte[] facialPointsDrawing(byte[] inputImage, byte[] facialPoints_b)
        {
            float[] facialPoints = Helper.BinaryToFloatArray(facialPoints_b);
            Stream jpegImageStream = new MemoryStream(inputImage);
            Image img = Image.FromStream(jpegImageStream);
            using (Graphics g = Graphics.FromImage(img))
            {
                using (Font arialFont = new Font("Arial", 10))
                {
                    for (int i = 0; i < facialPoints.Length / 2; i++)
                    {
                        PointF location = new PointF(facialPoints[i * 2] * img.Width, facialPoints[i * 2 + 1] * img.Height);
                        //string text = i.ToString();
                        g.DrawString(i.ToString(), arialFont, Brushes.Blue, location);
                    }
                }
            }

            //img.Save(@"C:\Users\yag\Source\Workspaces\Caffe\test\cropped\001_marked.jpg"); //Test;

            MemoryStream jpegFaceImageStream = new MemoryStream();

            img.Save(jpegFaceImageStream, ImageFormat.Jpeg);
            byte[] markedImage = jpegFaceImageStream.ToArray();
            return markedImage;
        }

        public static byte[] FloatArrayToBinary(float[] farray)
        {
            var r = new byte[farray.Length * sizeof(float)];
            Buffer.BlockCopy(farray, 0, r, 0, r.Length);
            return r;
        }
    }
}