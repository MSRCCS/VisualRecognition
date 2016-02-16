using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;
using FaceSdk;
using CaffeLibMC;

namespace CEERecognition
{
    public class CelebrityRecognitionResult
    {
        public class Rectangle
        {
            public int X, Y, Width, Height;
        }

        public class Celebrity
        {
            public string EntityName;
            public string EntityId;
            public string EntityDescription;
            public string EntityMUrl;
            public float Confidence;
        }

        public Rectangle Rect;
        public Celebrity[] RecogizedAs;
    }

    public class CelebrityPredictor
    {
        protected FaceDetectionJDA _faceDetector;
        protected CaffeModel _celebModel;
        protected FaceProcess _faceProcessor = new FaceProcess();

        protected string[] _labelMap = null;
        protected Dictionary<string, Tuple<string, string>> _entityInfo = null;

        Tuple<string, string, float[]>[] _knnModel = null;

        // for Knn search, label map file and entity info file are not needed
        public void Init(string faceModelFile,
                         string recogProtoFile, string recogModelFile, string recogLabelMapFile,
                         string entityInfoFile,
                         int gpu)
        {
            // Init face detection
            _faceDetector = new FaceDetectionJDA(new Model(faceModelFile));
            Console.WriteLine("Succeed: Load Face Detector!\n");

            // Init face processor
            _faceProcessor.LoadAlignmentTemplate();

            // Init face recognition
            string protoFile = Path.GetFullPath(recogProtoFile);
            string modelFile = Path.GetFullPath(recogModelFile);
            string labelMapFile = Path.GetFullPath(recogLabelMapFile);
            string curDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(Path.GetDirectoryName(recogProtoFile));
            CaffeModel.SetDevice(gpu);
            _celebModel = new CaffeModel(protoFile, modelFile);
            Directory.SetCurrentDirectory(curDir);
            Console.WriteLine("Succeed: Load Model File!\n");

            // Get label map
            if (!string.IsNullOrEmpty(recogLabelMapFile))
                _labelMap = File.ReadAllLines(recogLabelMapFile)
                    .Select(line => line.Split('\t')[0])
                    .ToArray();

            // Load entity info file
            if (!string.IsNullOrEmpty(entityInfoFile))
                _entityInfo = File.ReadAllLines(entityInfoFile)
                    .Select(line => line.Split('\t'))
                    .ToDictionary(f => f[1], f => new Tuple<string, string>(f[2], f[3]), StringComparer.OrdinalIgnoreCase);
        }

        public void InitKnn(string knnModelFile, int colName, int colMUrl, int colFeature)
        {
            _knnModel = File.ReadLines(knnModelFile)
                .Select(line =>
                {
                    var cols = line.Split('\t');
                    byte[] fea = Convert.FromBase64String(cols[colFeature]);
                    float[] feature = new float[fea.Length / sizeof(float)];
                    Buffer.BlockCopy(fea, 0, feature, 0, fea.Length);
                    Distance.Normalize(ref feature);
                    return new Tuple<string, string, float[]>(cols[colName], cols[colMUrl], feature);
                })
                .ToArray();
            int numPeople = _knnModel.Select(x => x.Item1).Distinct().Count();
            Console.WriteLine("KNN model loaded: {0} people and {1} faces", numPeople, _knnModel.Count());
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

        public float[] ExtractFeature(Bitmap bmp, string blobName)
        {
            return _celebModel.ExtractOutputs(bmp, blobName);
        }

        public float[][] ExtractFeature(Bitmap bmp, string[] blobNames)
        {
            return _celebModel.ExtractOutputs(bmp, blobNames);
        }

        public Tuple<string, float, int>[] SearchKnn(float[] feature, int topK, float minConfidence)
        {
            Distance.Normalize(ref feature);
            // search for the nearest neighbor
            var knn = _knnModel.Select((face, index) => new Tuple<string, float, int>(
                                face.Item1, Distance.DotProduct(feature, face.Item3), index))
                     .OrderByDescending(x => x.Item2)
                     .GroupBy(x => x.Item1)
                     .Select(g => g.First())
                     .Take(topK)
                     .Where(x => x.Item2 > minConfidence)
                     .ToArray();
            return knn;
        }

        public CelebrityRecognitionResult[] PredictKnn(Bitmap image, int knnTopK, float knnMinConfidence)
        {
            FaceRectLandmarks[] fdResult;
            List<Bitmap> croppedFaces;
            DetectAndCropFaces(image, out fdResult, out croppedFaces);

            CelebrityRecognitionResult[] recogResult = fdResult.Select((face, idx) =>
            {
                // resize
                var alignedCroppedFaceImageResized = _faceProcessor.ImageResize(croppedFaces[idx], 256, 256);

                // extract feature
                float[] feature = _celebModel.ExtractOutputs(alignedCroppedFaceImageResized, "fc6");

                var celebResult = new CelebrityRecognitionResult();
                celebResult.Rect = new CelebrityRecognitionResult.Rectangle() { X = face.FaceRect.Left, Y = face.FaceRect.Top, Width = face.FaceRect.Width, Height = face.FaceRect.Height };

                // get knn top K
                var topK = SearchKnn(feature, knnTopK, knnMinConfidence);
                // output
                celebResult.RecogizedAs = topK.Select(x => new CelebrityRecognitionResult.Celebrity()
                {
                    EntityName = x.Item1,
                    Confidence = x.Item2,
                    EntityDescription = string.Empty,
                    EntityId = string.Empty,
                    EntityMUrl = _knnModel[x.Item3].Item2
                }).ToArray();

                return celebResult;

            }).ToArray();

            return recogResult;
        }

        public CelebrityRecognitionResult[] Predict(Bitmap image, int dnnTopK, float dnnMinConfidence)
        {
            FaceRectLandmarks[] fdResult;
            List<Bitmap> croppedFaces;
            DetectAndCropFaces(image, out fdResult, out croppedFaces);

            CelebrityRecognitionResult[] recogResult = fdResult.Select((face, idx) =>
            {
                // resize
                var alignedCroppedFaceImageResized = _faceProcessor.ImageResize(croppedFaces[idx], 256, 256);

                // recognize
                float[] probs = _celebModel.ExtractOutputs(alignedCroppedFaceImageResized, "prob");

                var celebResult = new CelebrityRecognitionResult
                {
                    Rect =
                        new CelebrityRecognitionResult.Rectangle
                        {
                            X = face.FaceRect.Left,
                            Y = face.FaceRect.Top,
                            Width = face.FaceRect.Width,
                            Height = face.FaceRect.Height
                        }
                };

                // get model top K
                var topKResult = probs.Select((score, k) => new KeyValuePair<int, float>(k, score))
                                    .OrderByDescending(kv => kv.Value)
                                    .Take(dnnTopK).Where(kv => kv.Value > dnnMinConfidence);
                // outout
                celebResult.RecogizedAs = topKResult.Select(kv =>
                {
                    string entityId = _labelMap[kv.Key];
                    Tuple<string, string> entity = null;
                    _entityInfo.TryGetValue(entityId, out entity);
                    return new CelebrityRecognitionResult.Celebrity()
                    {
                        EntityName = entity == null ? string.Empty : entity.Item1,
                        EntityId = entityId,
                        EntityDescription = string.Empty,
                        Confidence = kv.Value,
                        EntityMUrl = entity == null ? string.Empty : entity.Item2
                    };
                }).ToArray();

                return celebResult;

            }).ToArray();

            return recogResult;
        }

    }

}
