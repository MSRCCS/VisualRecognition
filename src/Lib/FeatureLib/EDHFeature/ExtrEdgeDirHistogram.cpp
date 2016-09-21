//-----------------------------------------------------------------------------
// MSR China Development
//
// Microsoft Confidential.  All Rights Reserved.
//
// File: ExtrEdgeDirHistogram.cpp - the implementation of Edge direction 
//								    histogram feature extractor.
// 
// Owner: Yanfeng Sun, yfsun@microsoft.com
//			Lei Zhang
//
// Last Modified: March, 5, 2002.
//-----------------------------------------------------------------------------

#include "stdafx.h"
#include <math.h>

#include "Matrix.h"
#include "Feature.h"

#include "cannydetector.h"

//===========================================================================
//	ExtrEdgeDirHistogram
//		extract the edge dir histogram feature
//
//	Parameters
//		cfResult
//			receives the feature result 
//		cmIllumin
//			image presented in float matrix 
//
//	Return Values
//		S_OK indicates success. Otherwise for failure
//===========================================================================
HRESULT ExtrEdgeDirHistogram(CFloatMatrix& cmIllumin, CFeature& cfResult)
{
	HRESULT hr = E_INVALIDARG;

	if(cmIllumin.Width() !=0 && cmIllumin.Height() !=0)
	{
		CCannyDetector Detector;

		int nWidth = cmIllumin.Width();
		int nHeight = cmIllumin.Height();
		
		float *pImage = new float[nHeight*nWidth];
		
		if (pImage)
		{
			//copy the image into a continuous buffer
			for(int h=0; h<nHeight; h++)
			{
				float * pLine = cmIllumin.GetLineBuffer( h );
				memcpy( pImage + h * nWidth, pLine, sizeof(float) * nWidth);
			}

			hr = Detector.CannyDetect(pImage, cmIllumin.Width(), cmIllumin.Height());
			
			if (hr == S_OK)
			{
				float * pData = cfResult.GetFeatureBuffer();
				if (pData)
				{
					hr = Detector.GetDirHist( pData, NULL);
				}
				else
				{
					hr = E_FAIL;
				}
			}
			
			delete[] pImage;
		}
	}
	
	return hr;
}

