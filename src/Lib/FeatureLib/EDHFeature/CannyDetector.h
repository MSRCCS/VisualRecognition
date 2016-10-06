//-----------------------------------------------------------------------------
// MSR China Media Computing Group
//
// Copyright 1999-2002 Microsoft Corporation. All Rights Reserved.
//
// File: CannyDetector.h: interface for the CCannyDetector class.
// 
// Owner:	Yanfeng Sun  (yfsun@microsoft.com)
// 
// Last Modified: March 6, 2002 by Yanfeng Sun
//
//-----------------------------------------------------------------------------

#ifndef _CANNYDETECTOR_H_INCLUDED
#define _CANNYDETECTOR_H_INCLUDED

#if _MSC_VER > 1000
#pragma once
#endif // _MSC_VER > 1000

const float SIGMA = 1.0f;

//
// window size and sigma is preset
// window size should equal (int)(sigma*3*2)+1, 
// and if (windowSize %2 == 0) windowSize++;
// default sigma is 1 so the window size is 2*3+1 = 7
// we delibrately lengthen the window size to see the result
#define WINDOW_SIZE 7
	
#include "Feature.h"

class CCannyDetector  
{
public:
	CCannyDetector();
	~CCannyDetector();

	HRESULT CannyDetect(const float *pImage,const int iWidth, const int iHeight);
	HRESULT GetDirHist(float * pBuffer, int * pNumOfFloat);
	
private:
	float	Gaussian(float x);
	float	DGaussian(float x);
	void	Convolve1d(	const float *	pFilter, 
						const float *	pInImage, 
						float *			pOutImage, 
						const BOOL		bDir, 
						const int		iHeight, 
						const int		iWidth);
	
	float	FindHighThreshold(	float *			pImage, 
								const float		fEdgePercent, 
								const int		iHeight, 
								const int		iWidth);

	void	NonMaxSupp(	const float *	fDerriveX, 
						const float *	fDerriveY, 
						const float *	pImage, 
						BYTE *			pEdgeMap, 
						const int		iHeight, 
						const int		iWidth);

	void	CannyLinking(	const float *pImage, 
							BYTE *pEdgeMap, 
							const float fHighThresh, 
							const float fLowThreshRatio, 
							const int iHeight, 
							const int iWidth);

	void	EdgeDirHistogram(	const float *pDerivX, 
								const float *pDerivY,
								const BYTE *pEdgeMap, 
								float *pDirHist,
								const int iHeight, 
								const int iWidth);

	float 	m_fDirHist[LENGTH_EdgeDirHistogram];

	void	InitGaussianTab();

	static BOOL		m_bInitGaussianTab;
	static float	m_fGaussianTab[WINDOW_SIZE];
	static float	m_fDGaussianTab[WINDOW_SIZE];
};


#endif // end of _CANNYDETECTOR_H_INCLUDED
