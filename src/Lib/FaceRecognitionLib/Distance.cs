using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEERecognition
{
    public class Distance
    {
        static public float Norm(float[] f)
        {
            float sum = 0;
            for (int i = 0; i < f.Length; i++)
                sum += f[i] * f[i];
            return (float)Math.Sqrt(sum);
        }

        static public void Normalize(ref float[] f)
        {
            float norm = Norm(f);
            for (int i = 0; i < f.Length; i++)
                f[i] /= norm;
        }

        static public float L2Distance(float[] f1, float[] f2)
        {
            float score = 0;
            for (int i = 0; i < f1.Length; i++)
                score += (f1[i] - f2[i]) * (f1[i] - f2[i]);
            return score;
        }

        static public float L2NormedDistance(float[] f1, float[] f2)
        {
            Normalize(ref f1);
            Normalize(ref f2);
            return L2Distance(f1, f2);
        }

        static public float DotProduct(float[] f1, float[] f2)
        {
            float score = 0.0f;
            for (int i = 0; i < f1.Length; i++)
                score += f1[i] * f2[i];
            return score;
        }

        static public float CosineSimilarity(float[] f1, float[] f2)
        {
            float norm1 = Norm(f1);
            float norm2 = Norm(f2);
            float score = DotProduct(f1, f2) / norm1 / norm2;
            return score;
        }

        static public float CosineDistance(float[] f1, float[] f2)
        {
            // return a negative cosine similarity to turn it to a distance
            return -CosineSimilarity(f1, f2);
        }
    }
}
