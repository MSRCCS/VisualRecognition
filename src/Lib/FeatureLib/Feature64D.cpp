#include "stdafx.h"
#include <math.h>
#include "FlatImage.h"

//============================================================================
// CCorrCTMFeature feature Implementation
int QuantizeColorHSV44(BYTE bRed, BYTE bGreen, BYTE bBlue)
{
    static BYTE RGB2HSV[16][16][16];
    static bool bInit = false;
    if (!bInit)
    {
        for (int r = 0; r < 16; r ++)
        {
            for (int g = 0; g < 16; g ++)
            {
                for (int b = 0; b < 16; b ++)
                {
                    int nMax = max(r, max(g, b));
                    int nMin = min(r, min(g, b));

                    float h, s, v;
                    if (nMax == nMin)
                    {
                        h = 0;
                    }
                    else if (r == nMin)
                    {
                        h = 3.0f + (float)(b - g) / (nMax - nMin);
                    }
                    else if (g == nMin)
                    {
                        h = 5.0f + (float)(r - b) / (nMax - nMin);
                    }
                    else //if (b == nMin)
                    {
                        h = 1.0f + (float)(g - r) / (nMax - nMin);
                    }
                    if (nMax > 0)
                    {
                        s = (float)(nMax - nMin) / nMax;
                    }
                    else
                    {
                        s = 0;
                    }
                    v = (float)nMax / 15.0f;

                    int H = (int)(h * 60);
                    int S = (int)(s * 10);
                    int V = (int)(v * 10);
                    int nIndex;

                    if (V < 2)     // black area
                    {
                        nIndex = 0;
                    }
                    else if (S < 2) // Gray area
                    {
                        if (V < 8)
                        {
                            nIndex = V - 1; // 1~6, gray area
                        }
                        else
                        {
                            nIndex = 7 + (V >= 9);   // 7~8, white area
                        }
                    }
                    else // Color area
                    {
                        int nColor;
                        if (H < 22)
                        {
                            nColor = 0; // red
                        }
                        else if (H < 45)
                        {
                            nColor = 1; // orance
                        }
                        else if (H < 70)
                        {
                            nColor = 2; // yellow
                        }
                        else if (H < 155)
                        {
                            nColor = 3; // green
                        }
                        else if (H < 186)
                        {
                            nColor = 4; // cyan
                        }
                        else if (H < 278)
                        {
                            nColor = 5; // blue
                        }
                        else if (H < 330)
                        {
                            nColor = 6; // purple
                        }
                        else
                        {
                            nColor = 0; // red
                        }
                        
                        if (V >= 7) // SV plane is segmented into 5 regions
                        {
                            nIndex = 2 + (S >= 5) + (S >= 8);
                        }
                        else
                        {
                            nIndex = (S >= 7);
                        }
                        nIndex += 9 + 5 * nColor; // 9~43
                    }
                    RGB2HSV[r][g][b] = nIndex;
                }
            }
        }
        bInit = true;
    }
    return RGB2HSV[bRed / 16][bGreen / 16][bBlue / 16];
}

void NormalizeVector(float* pf, int nLen)
{
    if (pf != NULL && nLen > 0)
    {
        double f = 0;
		for (int i = 0; i < nLen; i ++)
        {
            f += pf[i] * pf[i];
        }
        f = sqrt(f);
        if (f < 1e-4f)
        {
            f = 1e-4f;
        }
        for (int i = 0; i < nLen; i ++)
        {
            pf[i] /= (float)f;
        }
    }
}

void ExtractCorrelogram44(BYTE* pbImage, int nWidth, int nHeight, float* pfFeature)
{
    float afHistogram[44], afCorrelogram[44];
    memset(afHistogram, 0, sizeof(afHistogram));
    memset(afCorrelogram, 0, sizeof(afCorrelogram));

    for (int y = 1; y < nHeight - 1; y ++)
    {
        BYTE* pbCurr = pbImage + y * nWidth;
        BYTE* pbPrev = pbCurr - nWidth;
        BYTE* pbNext = pbCurr + nWidth;

        for (int x = 1; x < nWidth - 1; x ++)
        {
            int nIndex = pbCurr[x];
            int nCount = (pbPrev[x - 1] == nIndex) + (pbPrev[x] == nIndex) + (pbPrev[x + 1] == nIndex)
                       + (pbCurr[x - 1] == nIndex) + (pbCurr[x + 1] == nIndex)
                       + (pbNext[x - 1] == nIndex) + (pbNext[x] == nIndex) + (pbNext[x + 1] == nIndex);
            afHistogram[nIndex] ++;
            afCorrelogram[nIndex] += nCount;
        }
    }

    for (int i = 0; i < 44; i ++)
    {
        if (afHistogram[i] > 0)
        {
            pfFeature[i] = afCorrelogram[i] / afHistogram[i];
        }
    }
    NormalizeVector(pfFeature, 44);
}

void ExtractTextureMoment(float* pfImage, int nWidth, int nHeight, float* pfFeature)
{
    const float fW2 = (float)sqrt(2.0);
    for (int y = 1; y < nHeight - 1; y ++)
    {
        float* pfCurr = pfImage + y * nWidth;
        float* pfPrev = pfCurr - nWidth;
        float* pfNext = pfCurr + nWidth;

        for (int x = 1; x < nWidth - 1; x ++)
        {
            float af[8];

            af[0] = pfPrev[x - 1] + pfPrev[x] + pfPrev[x + 1]
                    + pfCurr[x - 1] + pfCurr[x + 1]
                    + pfNext[x - 1] + pfNext[x] + pfNext[x + 1];

            af[1] = - pfPrev[x - 1] + pfPrev[x] - pfPrev[x + 1]
                    + pfCurr[x - 1] + pfCurr[x + 1]
                    - pfNext[x - 1] + pfNext[x] - pfNext[x + 1];

            af[2] = - pfPrev[x - 1] + pfPrev[x + 1] - pfNext[x - 1] + pfNext[x + 1]
                    + fW2 * (- pfCurr[x - 1] + pfCurr[x + 1]);

            af[3] = - pfPrev[x - 1] - pfPrev[x + 1] + pfNext[x - 1] + pfNext[x + 1]
                    + fW2 * (- pfPrev[x] + pfNext[x]);

            af[4] = fW2 * (- pfPrev[x] + pfCurr[x - 1] + pfCurr[x + 1] - pfNext[x]);

            af[5] = fW2 * (pfPrev[x - 1] - pfPrev[x + 1] - pfNext[x - 1] + pfNext[x + 1]);

            af[6] = pfPrev[x - 1] - pfPrev[x + 1] + pfNext[x - 1] - pfNext[x + 1]
                    + fW2 * (- pfCurr[x - 1] + pfCurr[x + 1]);

            af[7] = - pfPrev[x - 1] - pfPrev[x + 1] + pfNext[x - 1] + pfNext[x + 1]
                    + fW2 * (pfPrev[x] - pfNext[x]);

/*            float fValueNorm = af[0];
            if (fValueNorm < 1e-4f)
            {
                fValueNorm = 1e-4f;
            }
            fValueNorm = 1.0f / fValueNorm;
*/
            for (int i = 1; i < 8; i ++)
            {
//                af[i] *= fValueNorm;
                pfFeature[i - 1] += fabs(af[i]);
                pfFeature[i + 6] += af[i] * af[i];
            }
        }
    }

    float fSizeNorm = 1.0f / ((nWidth - 2) * (nHeight - 2));
    for (int i = 0; i < 7; i ++)
    {
        pfFeature[i] *= fSizeNorm;
        pfFeature[i + 7] = (float)sqrt(fabs(pfFeature[i + 7] * fSizeNorm - pfFeature[i] * pfFeature[i]));
    }
    NormalizeVector(pfFeature, 14);
}

int ExtractFeature64D(CFlatImage *pImage, void *pFeature)
{
	if (pFeature == 0)
	{
		return -1;
	}
    memset(pFeature, 0, sizeof(float) * 64);
    int nWidth = pImage->Width();
    int nHeight = pImage->Height();

    if (nWidth < 3 || nHeight < 3)
    {
        return -1;
    }

    float* pfCorrelogram44 = (float*)pFeature;
    float* pfTextureMoment = pfCorrelogram44 + 44;
    float* pfColorMoment = pfTextureMoment + 14;

    float* pfImage = new float[nWidth * nHeight];
    BYTE* pbImage = new BYTE[nWidth * nHeight];

    if (pbImage != NULL && pfImage != NULL)
    {
        for (int y = 0; y < nHeight; y ++)
        {
            float* pf = pfImage + y * nWidth;
            BYTE* pb = pbImage + y * nWidth;
            for (int x = 0; x < nWidth; x ++)
            {
				BYTE r = *pImage->GetRLayer(x, y);
				BYTE g = *pImage->GetGLayer(x, y);
				BYTE b = *pImage->GetBLayer(x, y);
                pf[x] =(float) r + g + b;
                pb[x] = QuantizeColorHSV44(r, g, b);

                pfColorMoment[0] += r;
                pfColorMoment[1] += g;
                pfColorMoment[2] += b;

                pfColorMoment[3] += r * r;
                pfColorMoment[4] += g * g;
                pfColorMoment[5] += b * b;
            }
        }

        ExtractCorrelogram44(pbImage, nWidth, nHeight, pfCorrelogram44);
        ExtractTextureMoment(pfImage, nWidth, nHeight, pfTextureMoment);

        float fArea =(float) nWidth * nHeight;
        for (int i = 0; i < 3; i ++)
        {
            pfColorMoment[i] /= fArea;
            pfColorMoment[i + 3] = pfColorMoment[i + 3] / fArea - pfColorMoment[i] * pfColorMoment[i];
            pfColorMoment[i + 3] = (float)sqrt(fabs(pfColorMoment[i + 3]));
        }
        NormalizeVector(pfColorMoment, 6);
    }
    if (pbImage != NULL)
    {
        delete []pbImage;
    }
    if (pfImage != NULL)
    {
        delete []pfImage;
    }

	return 0;
}

int ExtractFeature8x8(CFlatImage *pImage, void *pFeature)
{
	if (pFeature == 0)
	{
		return -1;
	}
    int size = 8;
	int nDimMean = size * size;
    memset(pFeature, 0, sizeof(float) * nDimMean);
    int nWidth = pImage->Width();
    int nHeight = pImage->Height();

    if (nWidth < 8 || nHeight < 8)
    {
        return -1;
    }
	int j, i;

	float* pfMean = (float*)pFeature;
    for (j = 0; j < size; j++)
    {
        int sv = nHeight * j / size;
        int ev = nHeight * (j + 1) / size;
        for (i = 0; i < size; i++)
        {
            int sh = nWidth * i / size;
            int eh = nWidth * (i + 1) / size;
            //pfMean[j * size + i] = 0.0f;
			int idx = j * size + i;
            for (int v = sv; v < ev; v++)
			{
                for (int h = sh; h < eh; h++)
                {
					BYTE r = *pImage->GetRLayer(h, v);
					BYTE g = *pImage->GetGLayer(h, v);
					BYTE b = *pImage->GetBLayer(h, v);
                    pfMean[idx] += r+g+b;
                }
			}
        }
    }
	
	NormalizeVector(pfMean, nDimMean);

	return 0;
}

int ExtractFeature8x8CM(CFlatImage *pImage, void *pFeature)
{
	if (pFeature == 0)
	{
		return -1;
	}
    int size = 8;
	int nDimMean = size * size;
    memset(pFeature, 0, sizeof(float) * nDimMean * 2);
    int nWidth = pImage->Width();
    int nHeight = pImage->Height();

    if (nWidth < 8 || nHeight < 8)
    {
        return -1;
    }
	int j, i;

	float* pfMean = (float*)pFeature;
	float* pfVar = pfMean + nDimMean;
    for (j = 0; j < size; j++)
    {
        int sv = nHeight * j / size;
        int ev = nHeight * (j + 1) / size;
        for (i = 0; i < size; i++)
        {
            int sh = nWidth * i / size;
            int eh = nWidth * (i + 1) / size;
            //pfFeature[j * size + i] = 0.0f;
			int idx = j * size + i;
            for (int v = sv; v < ev; v++)
			{
                for (int h = sh; h < eh; h++)
                {
					BYTE r = *pImage->GetRLayer(h, v);
					BYTE g = *pImage->GetGLayer(h, v);
					BYTE b = *pImage->GetBLayer(h, v);
					float gray = (float)r + g + b;
                    pfMean[idx] += gray;
					pfVar[idx] += gray * gray;
                }
			}

			float fArea = (float)(eh - sh) * (ev - sv);
			pfMean[idx] /= fArea;
			pfVar[idx] = pfVar[idx] / fArea - pfMean[idx] * pfMean[idx];
			pfVar[idx] = (float)sqrt(fabs(pfVar[idx]));
        }
    }

	NormalizeVector(pfMean, nDimMean * 2);

	return 0;
}

int ExtractFeature8x8CMColor(CFlatImage *pImage, void *pFeature)
{
	if (pFeature == 0)
	{
		return -1;
	}
    int size = 8;
	int nDimCM = 6;
    memset(pFeature, 0, sizeof(float) * size * size * nDimCM);
    int nWidth = pImage->Width();
    int nHeight = pImage->Height();

    if (nWidth < 8 || nHeight < 8)
    {
        return -1;
    }
	int j, i;

    for (j = 0; j < size; j++)
    {
        int sv = nHeight * j / size;
        int ev = nHeight * (j + 1) / size;
        for (i = 0; i < size; i++)
        {
            int sh = nWidth * i / size;
            int eh = nWidth * (i + 1) / size;
            //pfFeature[j * size + i] = 0.0f;
			int idx = j * size + i;
			float *pfColorMoment = (float *)pFeature + (j * size + i) * nDimCM;
            for (int v = sv; v < ev; v++)
			{
                for (int h = sh; h < eh; h++)
                {
					BYTE r = *pImage->GetRLayer(h, v);
					BYTE g = *pImage->GetGLayer(h, v);
					BYTE b = *pImage->GetBLayer(h, v);
					
					pfColorMoment[0] += r;
					pfColorMoment[1] += g;
					pfColorMoment[2] += b;

					pfColorMoment[3] += r * r;
					pfColorMoment[4] += g * g;
					pfColorMoment[5] += b * b;
                }
			}

			float fArea = (float)(eh - sh) * (ev - sv);

			for (int k = 0; k < 3; k++)
			{
				pfColorMoment[k] /= fArea;
				pfColorMoment[k + 3] = pfColorMoment[k + 3] / fArea - pfColorMoment[k] * pfColorMoment[k];
				pfColorMoment[k + 3] = (float)sqrt(fabs(pfColorMoment[k + 3]));
			}
			NormalizeVector(pfColorMoment, 6);
        }
    }

	NormalizeVector((float *)pFeature, size * size * nDimCM);

	return 0;
}

#include "EdhFeature\Matrix.h"
#include "EDHFeature\Feature.h"
HRESULT ExtrEdgeDirHistogram(CFloatMatrix& cmIllumin, CFeature& cfResult);

int ExtractFeatureEDH(CFlatImage *pImage, void *pFeature)
{
	if (pFeature == 0)
	{
		return -1;
	}
    memset(pFeature, 0, sizeof(float) * 13);	// edh dim = 13
    int nWidth = pImage->Width();
    int nHeight = pImage->Height();

    if (nWidth < 3 || nHeight < 3)
    {
        return -1;
    }

	// prepare float image
	CFloatMatrix floatImage;
	floatImage.Create(nWidth, nHeight);

	BYTE* pbImage = new BYTE[nWidth * nHeight];

    if (pbImage != NULL)
    {
        for (int y = 0; y < nHeight; y ++)
        {
			float * pLine = floatImage.GetLineBuffer( y );
            BYTE* pb = pbImage + y * nWidth;
            for (int x = 0; x < nWidth; x ++)
            {
				BYTE r = *pImage->GetRLayer(x, y);
				BYTE g = *pImage->GetGLayer(x, y);
				BYTE b = *pImage->GetBLayer(x, y);
                pLine[x] = ((float)r + g + b) / 3;
            }
        }

    }
    if (pbImage != NULL)
    {
        delete []pbImage;
    }

	// extract edh feature
	CFeature result;
	result.Init(FTYPE_EdgeDirHistogram);
	ExtrEdgeDirHistogram(floatImage, result);

	float* pfEdh = (float*)pFeature;
	memcpy(pfEdh, result.GetFeatureBuffer(), sizeof(float) * 13);

	return 0;
}
