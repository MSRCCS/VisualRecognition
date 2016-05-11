#pragma once

#include <math.h>
#include <vector>
#include <string>

#ifdef FACERECOGNITION_EXPORTS
#define FACERECOGNITION_EXPORT_API __declspec(dllexport)
#else
#define FACERECOGNITION_EXPORT_API __declspec(dllimport)
#endif

using std::vector;
using std::string;

namespace caffe
{
    class CaffeModel;
}
class LinearProjection;
class FaceTemplate27;

#ifndef BYTE
#define BYTE unsigned char
#endif
#ifndef UINT
#define UINT unsigned int
#endif

class FACERECOGNITION_EXPORT_API FaceRecognition
{
    caffe::CaffeModel *_caffeModel;
    int _caffeInputWidth;
    int _caffeInputHeight;
    char _caffeFeatureBlobName[100];

    LinearProjection *_ldaModel;
    int _ldaDim;

    FaceTemplate27 *_faceTemplate;

public:
    FaceRecognition();
    ~FaceRecognition();

    void Init(string &netFile, string &modelFile, string &ldaFile, int gpu_id);
    void SetFeatureBlobName(const string &blob);

    // input: 27 facial landmarks
    void ExtractFeature(const BYTE* image, UINT width, UINT height, UINT channels, UINT stride,
        vector<float> &landmarks, vector<float> &faceFeature);
};
