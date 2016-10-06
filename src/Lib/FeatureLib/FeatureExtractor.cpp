#include "StdAfx.h"
#include "FeatureExtractor.h"
#include "FlatImage.h"

#include <iostream>

int CFeatureExtractor::ExtrFeature64D(BYTE * pImgData, int iWidth, int iHeight, int iStride, int iBytesPerPixels, int iColorSequence, BOOL bTopDown, void *pFeature)
{
	CFlatImage img;
	RECT rctB = {0 , 0, iWidth, iHeight};
	BOOL rt = img.CreateImage(iWidth, iHeight, pImgData, bTopDown, iBytesPerPixels, iStride, iColorSequence, &rctB);

	if (rt == FALSE)
	{
#ifdef _DEBUG
		std::cout << "# Error in img.CreateImage()." << std::endl;
#endif
		return 4;
	}

	if( ExtractFeature64D( &img , (void*) pFeature ) != 0 )
	{
#ifdef _DEBUG
		std::cout << "# Error in ExtractFeature64D()." << std::endl;
#endif
		return 3;
	}

	return 0; // success
}

int CFeatureExtractor::ExtrFeature8x8(BYTE * pImgData, int iWidth, int iHeight, int iStride, int iBytesPerPixels, int iColorSequence, BOOL bTopDown, void *pFeature)
{
	CFlatImage img;
	RECT rctB = {0 , 0, iWidth, iHeight};
	BOOL rt = img.CreateImage(iWidth, iHeight, pImgData, bTopDown, iBytesPerPixels, iStride, iColorSequence, &rctB);

	if (rt == FALSE)
	{
#ifdef _DEBUG
		std::cout << "# Error in img.CreateImage()." << std::endl;
#endif
		return 4;
	}

	if( ExtractFeature8x8( &img , (void*) pFeature ) != 0 )
	{
#ifdef _DEBUG
		std::cout << "# Error in ExtractFeature8x8()." << std::endl;
#endif
		return 3;
	}

	return 0; // success
}

int CFeatureExtractor::ExtrFeature8x8CM(BYTE * pImgData, int iWidth, int iHeight, int iStride, int iBytesPerPixels, int iColorSequence, BOOL bTopDown, void *pFeature)
{
	CFlatImage img;
	RECT rctB = {0 , 0, iWidth, iHeight};
	BOOL rt = img.CreateImage(iWidth, iHeight, pImgData, bTopDown, iBytesPerPixels, iStride, iColorSequence, &rctB);

	if (rt == FALSE)
	{
#ifdef _DEBUG
		std::cout << "# Error in img.CreateImage()." << std::endl;
#endif
		return 4;
	}

	if( ExtractFeature8x8CM( &img , (void*) pFeature ) != 0 )
	{
#ifdef _DEBUG
		std::cout << "# Error in ExtractFeature8x8CM()." << std::endl;
#endif
		return 3;
	}

	return 0; // success
}

int CFeatureExtractor::ExtrFeature8x8CMColor(BYTE * pImgData, int iWidth, int iHeight, int iStride, int iBytesPerPixels, int iColorSequence, BOOL bTopDown, void *pFeature)
{
	CFlatImage img;
	RECT rctB = {0 , 0, iWidth, iHeight};
	BOOL rt = img.CreateImage(iWidth, iHeight, pImgData, bTopDown, iBytesPerPixels, iStride, iColorSequence, &rctB);

	if (rt == FALSE)
	{
#ifdef _DEBUG
		std::cout << "# Error in img.CreateImage()." << std::endl;
#endif
		return 4;
	}

	if( ExtractFeature8x8CMColor( &img , (void*) pFeature ) != 0 )
	{
#ifdef _DEBUG
		std::cout << "# Error in ExtractFeature8x8CM()." << std::endl;
#endif
		return 3;
	}

	return 0; // success
}

int CFeatureExtractor::ExtrFeature8x8_EDH_WHRatio(BYTE * pImgData, int iWidth, int iHeight, int iStride, int iBytesPerPixels, int iColorSequence, BOOL bTopDown, void *pFeature)
{
	CFlatImage img;
	RECT rctB = {0 , 0, iWidth, iHeight};
	BOOL rt = img.CreateImage(iWidth, iHeight, pImgData, bTopDown, iBytesPerPixels, iStride, iColorSequence, &rctB);

	if (rt == FALSE)
	{
#ifdef _DEBUG
		std::cout << "# Error in img.CreateImage()." << std::endl;
#endif
		return 4;
	}

	float *p8x8 = (float *)pFeature;
	ExtractFeature8x8( &img , (void*)p8x8);
	float *pEdh = (float*)p8x8 + 64;
	ExtractFeatureEDH( &img, (void *)pEdh);
	float *pWHRatio = (float*)p8x8 + 64 + 13;
	*pWHRatio = (float)iWidth / iHeight;
	
	return 0; // success
}

int CFeatureExtractor::ExtrFeature8x8_EDH2x2_WHRatio(BYTE * pImgData, int iWidth, int iHeight, int iStride, int iBytesPerPixels, int iColorSequence, BOOL bTopDown, void *pFeature)
{
	CFlatImage img;
	RECT rctB = {0 , 0, iWidth, iHeight};
	BOOL rt = img.CreateImage(iWidth, iHeight, pImgData, bTopDown, iBytesPerPixels, iStride, iColorSequence, &rctB);

	if (rt == FALSE)
	{
#ifdef _DEBUG
		std::cout << "# Error in img.CreateImage()." << std::endl;
#endif
		return 4;
	}

	float *p8x8 = (float *)pFeature;
	ExtractFeature8x8( &img , (void*)p8x8);

	if (iWidth >= 14 && iHeight >= 14) // Canny edge detection requires a Gaussian convolution. The kernel width is 7.
	{
		RECT rct1 = {0, 0, iWidth / 2, iHeight / 2};
		RECT rct2 = {iWidth / 2, 0, iWidth, iHeight / 2};
		RECT rct3 = {0, iHeight / 2, iWidth / 2, iHeight};
		RECT rct4 = {iWidth / 2, iHeight / 2, iWidth, iHeight};

		float *pEdh1 = p8x8 + 64;
		img.CreateImage(iWidth, iHeight, pImgData, bTopDown, iBytesPerPixels, iStride, iColorSequence, &rct1);
		ExtractFeatureEDH(&img, (void *)pEdh1);
		float *pEdh2 = pEdh1 + 13;
		img.CreateImage(iWidth, iHeight, pImgData, bTopDown, iBytesPerPixels, iStride, iColorSequence, &rct2);
		ExtractFeatureEDH(&img, (void *)pEdh2);
		float *pEdh3 = pEdh1 + 13 * 2;
		img.CreateImage(iWidth, iHeight, pImgData, bTopDown, iBytesPerPixels, iStride, iColorSequence, &rct3);
		ExtractFeatureEDH(&img, (void *)pEdh3);
		float *pEdh4 = pEdh1 + 13 * 3;
		img.CreateImage(iWidth, iHeight, pImgData, bTopDown, iBytesPerPixels, iStride, iColorSequence, &rct4);
		ExtractFeatureEDH(&img, (void *)pEdh4);
	}
	else
	{
		float *pEdh = p8x8 + 64;
		ExtractFeatureEDH(&img, (void *)pEdh);
	}

	float *pWHRatio = (float*)p8x8 + 64 + 13 * 4;
	*pWHRatio = (float)iWidth / iHeight;
	
	return 0; // success
}

int CFeatureExtractor::ExtrFeatureEDH2x2(BYTE * pImgData, int iWidth, int iHeight, int iStride, int iBytesPerPixels, int iColorSequence, BOOL bTopDown, void *pFeature)
{
	CFlatImage img;
	RECT rctB = {0 , 0, iWidth, iHeight};
	BOOL rt = img.CreateImage(iWidth, iHeight, pImgData, bTopDown, iBytesPerPixels, iStride, iColorSequence, &rctB);

	float *pfFeature = (float *)pFeature;

	if (iWidth >= 14 && iHeight >= 14) // Canny edge detection requires a Gaussian convolution. The kernel width is 7.
	{
		RECT rct1 = {0, 0, iWidth / 2, iHeight / 2};
		RECT rct2 = {iWidth / 2, 0, iWidth, iHeight / 2};
		RECT rct3 = {0, iHeight / 2, iWidth / 2, iHeight};
		RECT rct4 = {iWidth / 2, iHeight / 2, iWidth, iHeight};

		float *pEdh1 = pfFeature;
		img.CreateImage(iWidth, iHeight, pImgData, bTopDown, iBytesPerPixels, iStride, iColorSequence, &rct1);
		ExtractFeatureEDH(&img, (void *)pEdh1);
		float *pEdh2 = pEdh1 + 13;
		img.CreateImage(iWidth, iHeight, pImgData, bTopDown, iBytesPerPixels, iStride, iColorSequence, &rct2);
		ExtractFeatureEDH(&img, (void *)pEdh2);
		float *pEdh3 = pEdh1 + 13 * 2;
		img.CreateImage(iWidth, iHeight, pImgData, bTopDown, iBytesPerPixels, iStride, iColorSequence, &rct3);
		ExtractFeatureEDH(&img, (void *)pEdh3);
		float *pEdh4 = pEdh1 + 13 * 3;
		img.CreateImage(iWidth, iHeight, pImgData, bTopDown, iBytesPerPixels, iStride, iColorSequence, &rct4);
		ExtractFeatureEDH(&img, (void *)pEdh4);
	}
	else
	{
		float *pEdh = pfFeature;
		ExtractFeatureEDH(&img, (void *)pEdh);
	}

	return 0; // success
}


int CFeatureExtractor::ExtrFeature64D_EDH2x2(BYTE * pImgData, int iWidth, int iHeight, int iStride, int iBytesPerPixels, 
						   int iColorSequence, BOOL bTopDown, void *pFeature)
{
	CFlatImage img;
	RECT rctB = {0 , 0, iWidth, iHeight};
	BOOL rt = img.CreateImage(iWidth, iHeight, pImgData, bTopDown, iBytesPerPixels, iStride, iColorSequence, &rctB);

	ExtractFeature64D( &img , (void*) pFeature );
	float *pEDH2x2 = (float *)pFeature + 64;

	ExtrFeatureEDH2x2(pImgData, iWidth, iHeight, iStride, iBytesPerPixels, iColorSequence, bTopDown, (void *)pEDH2x2);

	return 0; // success
}

int CFeatureExtractor::ExtrFeature64D_EDH2x2_WHRatio(BYTE * pImgData, int iWidth, int iHeight, int iStride, int iBytesPerPixels, 
						   int iColorSequence, BOOL bTopDown, void *pFeature)
{
	CFlatImage img;
	RECT rctB = {0 , 0, iWidth, iHeight};
	BOOL rt = img.CreateImage(iWidth, iHeight, pImgData, bTopDown, iBytesPerPixels, iStride, iColorSequence, &rctB);

	ExtractFeature64D( &img , (void*) pFeature );
	float *pEDH2x2 = (float *)pFeature + 64;

	ExtrFeatureEDH2x2(pImgData, iWidth, iHeight, iStride, iBytesPerPixels, iColorSequence, bTopDown, (void *)pEDH2x2);

	float *pWHRatio = (float*)pFeature + 64 + 13 * 4;
	*pWHRatio = (float)iWidth / iHeight;
	
	return 0; // success
}

int CFeatureExtractor::ExtrFeature64D_EDH(BYTE * pImgData, int iWidth, int iHeight, int iStride, int iBytesPerPixels, 
						   int iColorSequence, BOOL bTopDown, void *pFeature)
{
	CFlatImage img;
	RECT rctB = {0 , 0, iWidth, iHeight};
	BOOL rt = img.CreateImage(iWidth, iHeight, pImgData, bTopDown, iBytesPerPixels, iStride, iColorSequence, &rctB);

	ExtractFeature64D( &img , (void*) pFeature );
	float *pEdh = (float*)pFeature + 64;
	ExtractFeatureEDH( &img, (void *)pEdh);

	return 0; // success
}
