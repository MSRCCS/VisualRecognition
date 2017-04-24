using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;
using FaceSdk;

namespace CEERecognition
{
    public class FaceDetector
    {
        readonly protected FaceDetectionJDA _faceDetector;
        readonly FaceProcess _faceProcessor = new FaceProcess();

        public FaceDetector(string faceModelFile)
        {
            _faceDetector = new FaceDetectionJDA(new Model(faceModelFile));

            // Init face processor
            _faceProcessor.LoadAlignmentTemplate();
        }

        public void DetectAndCropFaces(Bitmap bmp, out FaceRectLandmarks[] faceDetectionResult, out List<Bitmap> croppedFaces)
        {
            // 1. face detection
            int imgWidth = bmp.Width;
            int imgHeight = bmp.Height;
            faceDetectionResult = _faceDetector.DetectAndAlign(ImageUtility.LoadImageFromBitmapAsGray(bmp));

            // 2. face recognition
            croppedFaces = new List<Bitmap>();
            foreach (var fd in faceDetectionResult)
            {
                // get the face alignment result
                float[] landPoints = new float[fd.Landmarks.Points.Length * 2];
                for (int j = 0; j < fd.Landmarks.Points.Length; j++)
                {
                    landPoints[j * 2] = fd.Landmarks.Points[j].X / imgWidth;
                    landPoints[j * 2 + 1] = fd.Landmarks.Points[j].Y / imgHeight;
                }
                byte[] facialPoints = new byte[54 * 4];
                Buffer.BlockCopy(landPoints, 0, facialPoints, 0, facialPoints.Length);

                // crop
                var alignedCroppedFaceImage = _faceProcessor.faceAlignedCropping(bmp, facialPoints);

                croppedFaces.Add(alignedCroppedFaceImage);
            }
        }

    }

}
