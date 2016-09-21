//-----------------------------------------------------------------------------
// MSR China Media Computing Group
//
// Copyright 1999-2002 Microsoft Corporation. All Rights Reserved.
//
// File: CannyDetector.h: implementation for the CCannyDetector class.
// 
// Owner:	Yanfeng Sun  (yfsun@microsoft.com)
// 
// Last Modified: March 6, 2002 by Yanfeng Sun
//
//-----------------------------------------------------------------------------

#include <Windows.h>
#include "CannyDetector.h"
#include "math.h"
#include "Feature.h"

#include <stack>
using namespace std;

#define PI 3.1415926f

//
//The implementation for static members
//
BOOL	CCannyDetector::m_bInitGaussianTab = FALSE;
float	CCannyDetector::m_fGaussianTab[WINDOW_SIZE];
float	CCannyDetector::m_fDGaussianTab[WINDOW_SIZE];

//////////////////////////////////////////////////////////////////////
// Construction/Destruction
//////////////////////////////////////////////////////////////////////

CCannyDetector::CCannyDetector()
{
	if (!m_bInitGaussianTab)
	{
		InitGaussianTab();
	}
}

CCannyDetector::~CCannyDetector()
{
}

//===========================================================================
//InitGaussianTab
//	Initialize the gaussian and dgaussian table
//
//Parameters
//	None
//
//Return Values
//	None
//===========================================================================
void CCannyDetector::InitGaussianTab()
{
	for (int i=0;i<WINDOW_SIZE;i++) 
	{
		m_fGaussianTab[i] = (Gaussian(float(i-WINDOW_SIZE/2)-0.5f) 
							+Gaussian(float(i-WINDOW_SIZE/2)) 
							+Gaussian(float(i-WINDOW_SIZE/2)+0.5f))/3;
		m_fDGaussianTab[i] = DGaussian(float(i-WINDOW_SIZE/2));
	}
	m_bInitGaussianTab = TRUE;
}

//===========================================================================
//	CannyDetect
//		do canny detection
//
//	Parameters
//		pImage
//			a buffer to a gray image data present with float
//		iWidth
//			image width
//		iHeight
//			image height
//
///	Return Values
//		S_OK indicates success. Otherwise for failure
//===========================================================================
HRESULT CCannyDetector::CannyDetect(const float *pImage, int iWidth, int iHeight)
{
	HRESULT hr = S_OK;
	int i;

	if (iWidth <=0 || iHeight <= 0 || IsBadReadPtr( pImage, sizeof(float)*iWidth*iHeight))
	{
		hr = E_INVALIDARG;
	}

	// configurable const
	float fEdgePercent = 0.01f;
	float fLow2HighRatio = 0.4f;
	
	BYTE * pEdgePixels = NULL;

	if (hr == S_OK)
	{
		// initial the edge pixels array
		pEdgePixels = new BYTE[iHeight*iWidth];
		if (pEdgePixels)
		{
			// set zero
			memset(pEdgePixels, 0, iWidth * iHeight);
		}
		else
		{
			hr = E_OUTOFMEMORY;
		}
	}

	float * pTempImage= NULL;	
	float * pEdgeX = NULL;
	float * pEdgeY = NULL;
	if (hr == S_OK)
	{
		//allocate the memory for calculate 
		pEdgeX = new float[iHeight*iWidth];
		pEdgeY = new float[iHeight*iWidth];
		pTempImage = new float[iHeight*iWidth];

		if (pEdgeX == NULL || pEdgeY == NULL || pTempImage == NULL)
		{
			hr = E_OUTOFMEMORY;
		}
	}
	

	if (hr == S_OK)
	{
		//Convolve with Gaussian along 2 dimension *******

		// smooth the image with 2 d Gaussian kernel
		Convolve1d(m_fGaussianTab, pImage, pEdgeX, TRUE, iHeight, iWidth);
		Convolve1d(m_fGaussianTab, pEdgeX, pTempImage, FALSE, iHeight, iWidth);

		// convolve the blurred image with a derrive of Gaussian
		Convolve1d(m_fDGaussianTab, pTempImage, pEdgeX, TRUE, iHeight, iWidth);
		Convolve1d(m_fDGaussianTab, pTempImage, pEdgeY, FALSE, iHeight, iWidth);

		// fill the magnitude
		for (i=0;i<iHeight*iWidth;i++)
		{
			pTempImage[i] = float(sqrt(pEdgeX[i]*pEdgeX[i]+pEdgeY[i]*pEdgeY[i]));
		}
	
		float fHighThreshold = FindHighThreshold(pTempImage, fEdgePercent, iHeight, iWidth);
		
		if (fHighThreshold >= 0)
		{

			// the algorithm to determine the threshold
			// quantize and count
			NonMaxSupp(pEdgeX, pEdgeY, pTempImage, pEdgePixels, iHeight, iWidth);

			//extend the link area
			CannyLinking(pTempImage, pEdgePixels, fHighThreshold, fLow2HighRatio, iHeight, iWidth);

			// output the pTempImage into the bmp
			EdgeDirHistogram(pEdgeX, pEdgeY, pEdgePixels, m_fDirHist, iHeight, iWidth);
		}
		else
		{
			hr = E_FAIL;
		}
	}

	if (pTempImage)
	{
		delete []pTempImage;
		pTempImage = NULL;
	}
	if (pEdgeX)
	{
		delete []pEdgeX;
		pEdgeX = NULL;
	}
	if (pEdgeY)
	{
		delete []pEdgeY;
		pEdgeY = NULL;
	}
	if (pEdgePixels)
	{
		delete []pEdgePixels;
		pEdgePixels = NULL;
	}

	return hr;
}

//===========================================================================
//	Gaussian
//		calculate the gaussian function
//
//	Parameters
//		x
//			parameter for gaussian
//
///	Return Values
//		the value of gaussian function
//===========================================================================
float CCannyDetector::Gaussian(float x)
{
	// Window Size should be in odd number and the filter will not be normalized
	return float(exp((-x*x)/(2*SIGMA*SIGMA))/2/SIGMA/SIGMA/3.141593);
}
 
//===========================================================================
//	DGaussian
//		calculate the reversed gaussian function
//
//	Parameters
//		x
//			parameter for reversed gaussian
//
///	Return Values
//		the value of reversed gaussian function
//===========================================================================
float CCannyDetector::DGaussian(float x)
{
	return float(-x* exp(-x*x/(2*SIGMA*SIGMA))/ SIGMA/SIGMA);
}

//===========================================================================
//	Convolve1d
//		calculate convolve in one specified direction
//
//	Parameters
//		pFilter
//			pointer to the gaussian or dgaussian table
//		pInImage
//			pointer to the input image
//		pOutImage
//			pointer to the output image
//		bDir
//			convolve direction will depend on the bDir boolean value
//			if it is TRUE, the convolve along the X(j) direction
//			else the convolve took place along the Y(i) direction
//		iHeight
//			Image height
//		iWidth
//			Image Width
//
///	Return Values
//		None
//===========================================================================
void CCannyDetector::Convolve1d(const float *	pFilter, 
								const float *	pInImage, 
								float *			pOutImage, 
								const BOOL		bDir, 
								const int		iHeight, 
								const int		iWidth)
{
	int iHalfWin = WINDOW_SIZE/2;
	if (bDir) 
	{
		// convolve in x(j) direction
		for (int i=0;i<iHeight;i++) 
		{
			float * pOut = pOutImage + i * iWidth;
			const float * pIn = pInImage + i * iWidth;

			//j will loop from 0 to iWidth -1, but for the performance issue,
			//I seperate the loop into 3 pieces.
			for (int j=0;j<iHalfWin;j++) 
			{
				float sum=0;
				for (int k = -j ;k <= iHalfWin; k++) 
				{
					sum += pFilter[iHalfWin+k]*pIn[k];
				}
				* pOut = sum;
				pOut ++;
				pIn ++;
			}
			for (int j=iHalfWin;j<iWidth-iHalfWin;j++) 
			{
				float sum=0;
				for (int k = -iHalfWin;k <= iHalfWin; k++) 
				{
					sum += pFilter[iHalfWin+k]*pIn[k];
				}
				* pOut = sum;
				pOut ++;
				pIn ++;
			}
			for (int j=iWidth-iHalfWin;j<iWidth;j++) 
			{
				float sum=0;				
				for (int k = -iHalfWin;k <= iWidth - j - 1; k++) 
				{
					sum += pFilter[iHalfWin+k]*pIn[k];
				}
				* pOut = sum;
				pOut ++;
				pIn ++;
			}
		}
	} 
	else 
	{
		// convolve in x(j) direction
		for (int i=0;i<iHeight;i++) 
		{
			float * pOut = pOutImage + i * iWidth;
			const float * pIn = pInImage + i * iWidth;
			int start = ((i-iHalfWin)>=0?-iHalfWin:-i);
			int end = ((i+iHalfWin)<iHeight?iHalfWin:iHeight-i-1);
			for (int j=0;j<iWidth;j++) 
			{
				float sum=0;
				for (int k=start;k<=end;k++) 
				{
					sum += pFilter[iHalfWin+k]*pIn[k*iWidth];
				}
				* pOut= sum;
				pOut ++;
				pIn ++;
			}
		}
	}
}

//===========================================================================
//	FindHighThreshold
//		Find the threshold for edge
//
//	Parameters
//		pImage
//			pointer to the input image
//		fEdgePercent
//			percentage of pixels of edge in entire image
//		iHeight
//			Image height
//		iWidth
//			Image Width
//
///	Return Values
//		if the success, the return value is not below 0; otherwise, -1;
//===========================================================================
float CCannyDetector::FindHighThreshold(float		*pImage, 
										const float fEdgePercent, 
										const int	iHeight, 
										const int	iWidth)
{
	// first find the max value , assume all pImage is > 0
	float fMax = 0;
	int iPixels = iHeight * iWidth;
	int i;
	for (i=0;i<iPixels; i++)
	{
		if (pImage[i] > fMax) 
		{
			fMax = pImage[i];
		}
	}

	// quantize the image into 256 bin
	int iQuantLevel = 256;
	int *hist = new int[iQuantLevel];
	if (hist)
	{
		memset(hist, 0, sizeof(int) * iQuantLevel);

		int iTotalCount = 0;
		float fTemp = (iQuantLevel-1)/ fMax;

		for (i=0;i<iPixels;i++) 
		{
			if (pImage[i] != 0) 
			{
				iTotalCount++;
				pImage[i] *= fTemp;

				int iColor = int(pImage[i]);

				hist[iColor]++;
			}
		}

		// find the point that exceed the point percent
		int iAccumulate = 0;
		for (i=iQuantLevel-1;i>=0;i--) 
		{
			iAccumulate += hist[i];
			if (iAccumulate > (iTotalCount*fEdgePercent)) 
			{
				break;
			}
		}
		delete []hist;
		return i/float(iQuantLevel)*fMax;
	}
	else
		return -1.0f;	
}

//===========================================================================
//	NonMaxSupp
//		
//
//	Parameters
//		fDerriveX
//			Edge image for x direction
//		fDerriveY
//			edge image for y direction
//		pImage
//			pointer to the input image
//		pEdgeMap
//			the edge map
//		iHeight
//			Image height
//		iWidth
//			Image Width
//
///	Return Values
//		None
//===========================================================================
void CCannyDetector::NonMaxSupp(const float *	fDerriveX, 
								const float *	fDerriveY, 
								const float *	pImage, 
								BYTE *			pEdgeMap, 
								const int		iHeight, 
								const int		iWidth)
{

	for (int i=1;i<iHeight-1;i++)
	{
		int iTemp = i * iWidth;
		const float * pXBuf = fDerriveX + iTemp + 1;
		const float * pYBuf = fDerriveY + iTemp + 1;
		const float * pImgBuf = pImage + iTemp + 1;
		BYTE * pMapBuf = pEdgeMap + iTemp + 1;

		for (int j=1;j<iWidth-1;j++) 
		{
			float gx = *pXBuf; //as same as fDerriveX[i*iWidth+j];
			float gy = *pYBuf; //as same as fDerriveY[i*iWidth+j];
		
			float mag1;
			float mag2;
			
			// linear interpolation using the tagent of the gradient vector
			if (gx>=0) 
			{
				if (gy>=0) 
				{
					if (gx >= gy) 
					{
						float d = gy/gx;
						//as same as pImage[i*iWidth+j+1]*(1-d) + pImage[(i+1)*iWidth+j+1]*d;
						mag1 = pImgBuf[1] * (1-d) + pImgBuf[iWidth + 1] * d;
						//as same as pImage[i*iWidth+j-1]*(1-d) + pImage[(i-1)*iWidth+j-1]*d;
						mag2 = pImgBuf[-1]* (1-d) + pImgBuf[-iWidth - 1] * d;
					} 
					else 
					{ // gx < gy
						float d = gx/gy;
						//as same as pImage[(i+1)*iWidth+j]*(1-d) + pImage[(i+1)*iWidth+j+1]*d;
						mag1 = pImgBuf[iWidth]*(1-d) + pImgBuf[iWidth+1]*d;
						//as same as pImage[(i-1)*iWidth+j]*(1-d) + pImage[(i-1)*iWidth+j-1]*d;
						mag2 = pImgBuf[-iWidth]*(1-d) + pImgBuf[-iWidth-1]*d;
					}
				} 
				else // gy < 0
				{ 
					if (gx >= -gy) 
					{
						float d = -gy/gx;
						//as same as pImage[i*iWidth+j+1]*(1-d) + pImage[(i-1)*iWidth+j+1]*d;
						mag1 = pImgBuf[1]*(1-d) + pImgBuf[-iWidth+1]*d;
						//as same as pImage[i*iWidth+j-1]*(1-d) + pImage[(i+1)*iWidth+j-1]*d;
						mag2 = pImgBuf[-1]*(1-d) + pImgBuf[iWidth-1]*d;
					} 
					else // gx <= -gy
					{ 
						float d = -gx/gy;
						//as same as pImage[(i-1)*iWidth+j]*(1-d) + pImage[(i-1)*iWidth+j+1]*d;
						mag1 = pImgBuf[-iWidth]*(1-d) + pImgBuf[-iWidth+1]*d;
						//as same as pImage[(i+1)*iWidth+j]*(1-d) + pImage[(i+1)*iWidth+j-1]*d;
						mag2 = pImgBuf[iWidth]*(1-d) + pImgBuf[iWidth-1]*d;
					}
				}
			} 
			else // gx < 0
			{ 
				if (gy >= 0) 
				{
					if (gy >= -gx) 
					{
						float d = -gx/gy;
						//as same as pImage[(i+1)*iWidth+j]*(1-d) + pImage[(i+1)*iWidth+j-1]*d;
						mag1 = pImgBuf[iWidth]*(1-d) + pImgBuf[iWidth-1]*d;
						//as same as pImage[(i-1)*iWidth+j]*(1-d) + pImage[(i-1)*iWidth+j+1]*d;
						mag2 = pImgBuf[-iWidth]*(1-d) + pImgBuf[-iWidth+1]*d;
					} 
					else // gy < -gx
					{ 
						float d = -gy/gx;
						//as same as pImage[i*iWidth+j-1]*(1-d) + pImage[(i+1)*iWidth+j-1]*d;
						mag1 = pImgBuf[-1]*(1-d) + pImgBuf[iWidth-1]*d;
						//as same as pImage[i*iWidth+j+1]*(1-d) + pImage[(i-1)*iWidth+j+1]*d;
						mag2 = pImgBuf[1]*(1-d) + pImgBuf[-iWidth+1]*d;
					}
				} 
				else // gy < 0
				{ 
					if (-gx >= -gy) 
					{
						float d = gy/gx;
						//as same as pImage[i*iWidth+j-1]*(1-d) + pImage[(i-1)*iWidth+j-1]*d;
						mag1 = pImgBuf[-1]*(1-d) + pImgBuf[-iWidth-1]*d;
						//as same as pImage[i*iWidth+j+1]*(1-d) + pImage[(i+1)*iWidth+j+1]*d;
						mag2 = pImgBuf[1]*(1-d) + pImgBuf[iWidth+1]*d;
					} 
					else // -gx < -gy
					{ 
						float d = gx/gy;
						//as same as pImage[(i-1)*iWidth+j]*(1-d) + pImage[(i-1)*iWidth+j-1]*d;
						mag1 = pImgBuf[-iWidth]*(1-d) + pImgBuf[-iWidth-1]*d;
						//as same as pImage[(i+1)*iWidth+j]*(1-d) + pImage[(i+1)*iWidth+j+1]*d;
						mag2 = pImgBuf[iWidth]*(1-d) + pImgBuf[iWidth+1]*d;
					}
				}
			}

			float m00 = *pImgBuf;//as same as pImage[i*iWidth+j];

			if ((m00 > mag1) && (m00 > mag2)) 
			{
				//as same as pEdgeMap[i*iWidth+j] 
				* pMapBuf = 128;
			}
			pMapBuf ++;
			pImgBuf ++;
			pXBuf ++;
			pYBuf ++;
		}
	}
}

//===========================================================================
//	CannyLinking
//		extend the edge area
//
//	Parameters
//		pImage
//			pointer to the input image
//		pEdgeMap
//			the edge map
//		iHeight
//			Image height
//		iWidth
//			Image Width
//		fHighThresh
//			the threshold for starting point
//		fLowThreshRatio
//			the threshold for extending point
///	Return Values
//		None
//===========================================================================
void CCannyDetector::CannyLinking(const float *pImage, 
								  BYTE *pEdgeMap, 
								  const float fHighThresh, 
								  const float fLowThreshRatio, 
								  const int iHeight, 
								  const int iWidth)
{
	float fLowThreshold = fHighThresh*fLowThreshRatio;

	// the standard edge grow algorithm to follow the edge
	for (int i=1;i<iHeight-1;i++) 
	{
		const float * pImgBuf = pImage + i*iWidth + 1;
		BYTE *	pMapBuf = pEdgeMap + i * iWidth + 1;
		for (int j=1;j<iWidth-1;j++) 
		{
			// Traverse the whole m_edgelArray 
			if (*pMapBuf == 128 && 
				*pImgBuf > fHighThresh) 
			{
				stack<POINT> pposStack;
				POINT pt;
				pt.x = i;
				pt.y = j;
				pposStack.push(pt);

				while (!pposStack.empty()) 
				{
					POINT thePoint = pposStack.top();
					pposStack.pop();

					pEdgeMap[thePoint.x*iWidth+thePoint.y] = 255;

					for (int r=thePoint.x-1;r<=thePoint.x+1;r++) 
					{
						for (int c=thePoint.y-1;c<=thePoint.y+1;c++) 
						{
							if (0 <= r && r<iHeight && 0<=c && c<iWidth) 
							{
								if (pEdgeMap[r*iWidth+c] ==128 && 
									pImage[r*iWidth+c] > fLowThreshold) 
								{
									POINT pt;
									pt.x = r;
									pt.y = c;
									pposStack.push(pt);
								}
							}
						}
					}
				}
                
			}
			pMapBuf ++;
			pImgBuf ++;
		}
	}
}

//===========================================================================
//	EdgeDirHistogram
//		get the edge histogram
//
//	Parameters
//		pDerivX
//			edge image for x direction
//		pDerivY
//			edge image for y direction
//		pEdgeMap
//			the edge map
//		iHeight
//			Image height
//		iWidth
//			Image Width
//		pDirHist
//			pointer to the buffer to receive the histogram result
///	Return Values
//		None
//===========================================================================
void CCannyDetector::EdgeDirHistogram(const float *pDerivX, 
									  const float *pDerivY,
									  const BYTE *pEdgeMap, 
									  float *pDirHist,
									  const int iHeight, 
									  const int iWidth)
{
	int i,j, iEdgeCounter;
	
	
	int iInterval = 360 / (LENGTH_EdgeDirHistogram - 1);
	int iDirCounter[LENGTH_EdgeDirHistogram];

	memset( iDirCounter, 0, sizeof(int) * LENGTH_EdgeDirHistogram);

	iEdgeCounter = 0;

	int iPixels = iWidth * iHeight;
	float fTemp = 360.0f/(2*PI);

	for (i=0; i<iPixels;i++)
	{
		if (pEdgeMap[i]==255)
		{
			iEdgeCounter ++;

			float fArc = float(atan2(pDerivY[i],pDerivX[i]) + PI);
			float fDeg= fTemp*fArc;

			if (fDeg == 360) 
			{
				iDirCounter[LENGTH_EdgeDirHistogram-2] ++;
			}
			else 
			{
				j = (int)(fDeg/iInterval);
				iDirCounter[j] ++;
			}
		}
		else 
		{
			iDirCounter[LENGTH_EdgeDirHistogram-1] ++;
		}
	}
	
	for (j=0; j<LENGTH_EdgeDirHistogram-1; j++)
	{
		if (iEdgeCounter!=0)
		{
			pDirHist[j] = float(iDirCounter[j])/iEdgeCounter;
		}
		else 
		{
			pDirHist[j] = 0.0;
		}
	}
	
	pDirHist[LENGTH_EdgeDirHistogram-1] = float(iDirCounter[LENGTH_EdgeDirHistogram-1])/iPixels;
}




HRESULT CCannyDetector::GetDirHist(float * pBuffer, int * pNumOfFloat)
{
	HRESULT hr = E_INVALIDARG;
	if (pBuffer == NULL)
	{
		if (pNumOfFloat != NULL)
		{
			*pNumOfFloat = LENGTH_EdgeDirHistogram;
			hr = S_OK;
		}
	}
	else
	{
		if (pNumOfFloat)
		{
			*pNumOfFloat = LENGTH_EdgeDirHistogram;
		}
		if (!IsBadWritePtr( pBuffer, sizeof(float)*LENGTH_EdgeDirHistogram))
		{
			memcpy( pBuffer, m_fDirHist, sizeof(float)*LENGTH_EdgeDirHistogram);
			hr = S_OK;
		}
	}
	return hr;
}