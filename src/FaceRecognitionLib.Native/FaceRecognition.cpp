// FaceRecognition.cpp : Defines the exported functions for the DLL application.
//
#include "FaceRecognition.h"
#include "caffe.native.h"

class PointData
{
    vector<float> m_Points;
public:
    PointData(const PointData &refShape)
    {
        m_Points = refShape.m_Points;
    }
    PointData(int numPoints)
    {
        m_Points.resize(numPoints * 2);
        memset(&m_Points[0], 0, sizeof(float) * m_Points.size());
    }
    PointData(const vector<float> &points)
    {
        m_Points = points;
    }

    int PointNum() { return (int)m_Points.size() / 2; }
    float X(int index) { return m_Points[index * 2]; }
    float Y(int index) { return m_Points[index * 2 + 1]; }
    void SetX(int index, float value) { m_Points[index * 2] = value; }
    void SetY(int index, float value) { m_Points[index * 2 + 1] = value; }
    vector<float>& Data() { return m_Points; }

    float Norm2()
    {
        double norm = 0;
        for (int i = 0; i < m_Points.size(); i++)
            norm += m_Points[i] * m_Points[i];
        return (float)sqrt(norm);
    }

    void Normalize()
    {
        float centerx, centery, norm;
        GetCenterPoint(centerx, centery);
        Translate(-centerx, -centery);
        norm = Norm2();
        Scale(1 / norm);
    }

    // geometric transformation
    void Scale(float s)
    {
        for (int i = 0; i < m_Points.size(); i++)
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

    void GetCenterPoint(float &x, float &y)
    {
        int numPoints = PointNum();
        x = 0; y = 0;
        if (m_Points.size() == 0) return;
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
    void AlignNorm2(const PointData &refShape)
    {
        PointData refCopy(refShape);

        // Translate both shapecenter to (0,0) 
        float refCenterx, refCentery;
        refCopy.GetCenterPoint(refCenterx, refCentery);
        refCopy.Translate(-refCenterx, -refCentery);
        float centerx, centery;
        GetCenterPoint(centerx, centery);
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

    void AlignNorm2(const PointData &refShape, float &a, float &b)
    {
        PointData refCopy(refShape);
        a = 1.0f;
        b = 0.0f;

        // Translate both shapecenter to (0,0) 
        float refCenterx, refCentery;
        refCopy.GetCenterPoint(refCenterx, refCentery);
        refCopy.Translate(-refCenterx, -refCentery);
        float centerx, centery;
        GetCenterPoint(centerx, centery);
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
    }
};

class FaceTemplate27
{
private:
    const int facialPointsNum = 27;
    vector<float> templatePoints;
public:
    FaceTemplate27()
    {
        double templatePoints_d[] = { -0.1435745, -0.09751324, 0.1465635, -0.09545494, -0.002502798, 0.08669894, -0.134234, 0.2176705, 0.1327966, 0.2200314, -0.2597697, -0.1601567, -0.06860603, -0.1588396, -0.195908, -0.09441547, -0.1457139, -0.1164698, -0.1468758, -0.07554296, -0.09656556, -0.08993812, 0.06787405, -0.1587539, 0.2640095, -0.1565289, 0.09899407, -0.0884045, 0.1486475, -0.1143898, 0.1493653, -0.073224, 0.1989327, -0.09132996, -0.04037917, -0.08282474, 0.04079287, -0.08254138, -0.06442133, 0.03188311, 0.06242511, 0.03322322, -0.09343174, 0.09200199, 0.09143509, 0.09396633, -0.002061998, 0.1931304, -0.002255796, 0.2202828, -0.002566518, 0.2530539, -0.002969528, 0.2943854 };
        _ASSERT(sizeof(templatePoints_d) / sizeof(double) == facialPointsNum * 2);
        templatePoints.resize(facialPointsNum * 2);
        for (int x = 0; x < templatePoints.size(); x++)
        {
            templatePoints[x] = (float)templatePoints_d[x];
        }
    }

    float GetTemplateFacialWidth()
    {
        return abs(templatePoints[2] - templatePoints[0]);
    }

    // a and b for rotation
    void CalcCroppingParams(const vector<float> &facialPoints,
        float &facialCenterX, float &facialCenterY, float &a, float &b, float &facialWidth)
    {
        facialWidth = GetTemplateFacialWidth();

        PointData facialP(facialPoints);
        facialP.GetCenterPoint(facialCenterX, facialCenterY);

        PointData templateP(templatePoints);
        facialP.AlignNorm2(templateP, a, b);
    }
};

class LinearProjection
{
private:
    vector<float> _pcaMatrix;
    int _cols, _rows;

public:
    int Init(const string &ldaFile, int rowNum)
    {
        FILE *fp;
        int err = fopen_s(&fp, ldaFile.c_str(), "rb");
        if (err == 0)
        {
            fseek(fp, 0, SEEK_END);
            long size = ftell(fp);
            fseek(fp, 0, SEEK_SET);
            _ASSERT(size % sizeof(float) == 0);
            _rows = rowNum;
            _cols = size / sizeof(float) / _rows;
            _pcaMatrix.resize(size / sizeof(float));
            size_t size_read = fread(&_pcaMatrix[0], 1, size, fp);
            if (size_read < size)
                err = -1;
            fclose(fp);
        }

        return err;
    }

    vector<float> RightMatrixProduct(const float *feature, int pcaDim)
    {
        vector<float> result(pcaDim);
        for (int i = 0; i < pcaDim; i++)
        {
            float *pca_row = &_pcaMatrix[0] + _cols * i;
            result[i] = dot(feature, pca_row, _cols);
        }

        return result;
    }

    void L2Normalize(vector<float> &feature)
    {
        float norm = 0;
        for (int i = 0; i < feature.size(); i++)
            norm += feature[i] * feature[i];
        norm = sqrt(norm);
        for (int i = 0; i < feature.size(); i++)
            feature[i] /= norm;
    }

private:
    float dot(const float* left, const float* right, int dim)
    {
        float result = 0;
        for (int i = 0; i < dim; i++)
        {
            result += left[i] * right[i];
        }
        return result;
    }
};

FaceRecognition::FaceRecognition()
{
    _caffeModel = new caffe::CaffeModel();
    _ldaModel = new LinearProjection();
    _faceTemplate = new FaceTemplate27();
}

FaceRecognition::~FaceRecognition()
{
    delete _caffeModel;
    delete _ldaModel;
    delete _faceTemplate;
}

void FaceRecognition::Init(string &netFile, string &modelFile, string &ldaFile, int gpu_id)
{
    _caffeModel->SetDevice(gpu_id);
    // init caffe
    _caffeModel->Init(netFile, modelFile);
    _caffeInputWidth = _caffeModel->GetInputImageWidth();
    _caffeInputHeight = _caffeModel->GetInputImageHeight();
    SetFeatureBlobName("fc6");

    // init lda
    _ldaDim = 512;
    _ldaModel->Init(ldaFile, _ldaDim);
}

void FaceRecognition::SetFeatureBlobName(const string &blob)
{
    strcpy_s(_caffeFeatureBlobName, sizeof(_caffeFeatureBlobName) - 1, blob.c_str());
}

// input: 27 facial landmarks
void FaceRecognition::ExtractFeature(const BYTE* image, UINT width, UINT height, UINT channels, UINT stride,
    vector<float> &landmarks, vector<float> &faceFeature)
{
    // 1. facial landmarks => cropping params
    float centerX, centerY;
    float a, b;
    float refFacialWidth;
    _faceTemplate->CalcCroppingParams(landmarks, centerX, centerY, a, b, refFacialWidth);

    // 2. crop and resize face region
    float scale = sqrt(a * a + b * b);
    float rotationRadius = atan(b / (a + 1E-20f));
    float baseWidth = refFacialWidth / scale;
    string imageData;
    caffe::CropImageAndResize(image, width, height, channels, stride,
        imageData, _caffeInputWidth, _caffeInputHeight,
        centerX, centerY, rotationRadius, baseWidth,
        1.75, 1.75, 1.5, 1.5);

    // 3. extract caffe feature
    caffe::FloatArray caffeFeature = _caffeModel->ExtractOutputs(imageData, _caffeFeatureBlobName);

    // 4. apply lda transform
    faceFeature = _ldaModel->RightMatrixProduct(caffeFeature.Data, _ldaDim);
    _ldaModel->L2Normalize(faceFeature);
}
