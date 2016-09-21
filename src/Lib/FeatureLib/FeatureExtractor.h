#pragma once

//#include "resource.h"

class CFeatureExtractor
{
	// 52D, 2x2 EDH: 13*4=52
	static int ExtrFeatureEDH2x2(BYTE * pImgData, int iWidth, int iHeight, int iStride, int iBytesPerPixels, 
						   int iColorSequence, BOOL bTopDown, void *pFeature);
public:
	CFeatureExtractor(void) {}
	~CFeatureExtractor(void) {}
	
	// 64D, Correlogram:44D + CM:6D + ColorTextureMoment: 14D
	static int ExtrFeature64D(BYTE * pImgData, int iWidth, int iHeight, int iStride, int iBytesPerPixels, 
						   int iColorSequence, BOOL bTopDown, void *pFeature);
	
	// 64D, Mean values from each 8x8 grids in gray image (for duplicate detection)
	static int ExtrFeature8x8(BYTE * pImgData, int iWidth, int iHeight, int iStride, int iBytesPerPixels, 
						   int iColorSequence, BOOL bTopDown, void *pFeature);
	
	// 128D, ColorMoment from each 8x8 grids in gray image (for duplicate detection)
	static int ExtrFeature8x8CM(BYTE * pImgData, int iWidth, int iHeight, int iStride, int iBytesPerPixels, 
						   int iColorSequence, BOOL bTopDown, void *pFeature);

	// 384D, ColorMoment from each 8x8 grids in color image (for duplicate detection)
	static int ExtrFeature8x8CMColor(BYTE * pImgData, int iWidth, int iHeight, int iStride, int iBytesPerPixels, 
						   int iColorSequence, BOOL bTopDown, void *pFeature);

	// 78D, 8x8 grids + EDH + WH (for duplicate detection)
	static int ExtrFeature8x8_EDH_WHRatio(BYTE * pImgData, int iWidth, int iHeight, int iStride, int iBytesPerPixels, 
						   int iColorSequence, BOOL bTopDown, void *pFeature);

	// 117D, 8x8 grids + 2x2 EDH + WH (for duplicate detection): 64+13*4+1=117
	static int ExtrFeature8x8_EDH2x2_WHRatio(BYTE * pImgData, int iWidth, int iHeight, int iStride, int iBytesPerPixels, 
						   int iColorSequence, BOOL bTopDown, void *pFeature);

	// 116D, 64D + 2x2 EDH (for similar search): 64+13*4=116
	static int ExtrFeature64D_EDH2x2(BYTE * pImgData, int iWidth, int iHeight, int iStride, int iBytesPerPixels, 
						   int iColorSequence, BOOL bTopDown, void *pFeature);
	// 117D, 64D + 2x2 EDH + WH (for similar search): 64+13*4+1=117
	static int ExtrFeature64D_EDH2x2_WHRatio(BYTE * pImgData, int iWidth, int iHeight, int iStride, int iBytesPerPixels, 
						   int iColorSequence, BOOL bTopDown, void *pFeature);

	// 77D, 64D + EDH (for similar search): 64+13=77
	static int ExtrFeature64D_EDH(BYTE * pImgData, int iWidth, int iHeight, int iStride, int iBytesPerPixels, 
						   int iColorSequence, BOOL bTopDown, void *pFeature);

};

