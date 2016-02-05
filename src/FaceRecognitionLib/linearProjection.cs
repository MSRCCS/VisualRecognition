using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace CEERecognition
{
    public class linearProjection
    {
        //private int rows, cols;
        private float[][] pca_matrix;
        //private int pcaDimension;

        public byte[] rightMatrixProduct(byte[] binFeature, int pcaDimension)
        {
            //pcaDimension = input_pcaDimension;
            //readRightMatrixBinaryRowNum(path, pcaDimension);

            //float[] feature = DataConvertBinaryToFloatArray(binFeature);
            float[] feature = ConvertBinaryToFloatArray(binFeature);


            float[] result = new float[pcaDimension];
            for (int i = 0; i < pcaDimension; i++)
            {
                result[i] = dot(feature, pca_matrix[i]);
            }

            byte[] result_buffer = new byte[result.Length * 4];
            Buffer.BlockCopy(result, 0, result_buffer, 0, result_buffer.Length);
            return result_buffer;
        }
        private float dot(float[] left, float[] right)
        {
            float result = 0;
            for (int i = 0; i < left.Length; i++)
            {
                result += left[i] * right[i];
            }
            return result;
        }
        public void readRightMatrixBinaryRowNum(string path, int rowNum)
        {
            FileStream stream = File.OpenRead(path);

            byte[] fileBytes = new byte[(int)(stream.Length)];

            stream.Read(fileBytes, 0, (int)stream.Length);

            pca_matrix = new float[rowNum][];
            //float[][] eigenV = new float[dataSize][];
            int rowSize = (int)stream.Length / (rowNum * 4);
            byte[] fb = new byte[rowSize * 4];
            for (int i = 0; i < rowNum; i++)
            {
                Buffer.BlockCopy(fileBytes, i * rowSize * 4, fb, 0, fb.Length);
                pca_matrix[i] = ConvertBinaryToFloatArray(fb);
            }
        }

        public static float[] DataConvertBinaryToFloatArray(byte[] arr)
        {
            if (arr == null)
                return null;

            if (arr.Length % 4 != 0)
            {
                throw new System.Exception("input arr[] does not have 4*x length");
            }

            float[] f = new float[arr.Length];

            for (int i = 0; i < arr.Length; i++)
            {
                f[i] = Convert.ToSingle(arr[i]);
            }
            //Buffer.BlockCopy(arr, 0, f, 0, arr.Length);
            return f;
        }

        public static float[] ConvertBinaryToFloatArray(byte[] arr)
        {
            if (arr == null)
                return null;

            if (arr.Length % 4 != 0)
            {
                throw new System.Exception("input arr[] does not have 4*x length");
            }

            float[] f = new float[arr.Length / 4];
            Buffer.BlockCopy(arr, 0, f, 0, arr.Length);
            return f;
        }

    }
}
