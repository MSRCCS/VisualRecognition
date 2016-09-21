	//-----------------------------------------------------------------------------
// MSR China Media Computing Group
//
// Copyright 1999-2001 Microsoft Corporation. All Rights Reserved.
//
// File: FlatImage.h - Defines a flag image class. 
// 
// Owner:  Yanfeng Sun, Lei Zhang
//
// Last Modified: May, 8, 2002 by Yanfeng
//-----------------------------------------------------------------------------

#ifndef _FLATIMAGE_H_
#define _FLATIMAGE_H_

#include <vector>
#include <assert.h>
#include <math.h>
// Image struct for local use
class CFlatImage
{
// Data
    int m_iWidth;	//For CFlatImage can be part of a large image, these two
    int m_iHeight;	//value just show the width and height of the sub-image
	std::vector<BYTE *> m_paLineBuffer;	//pointers to each scan line
    
//info for source image
	BYTE* m_pbImage;		//Image Buffer
	int m_iImageWidth;		//the size of the full image pointed by m_pbImage. It is
	int m_iImageHeight;		//equal or larger than m_iWidth and m_iHeight

	int m_iColorSequence;
	int m_iRedOffset;		//the sequence of color, RGB or BGR		
	int m_iGreenOffset;
	int m_iBlueOffset;

	int m_iPixelBytes;		//should be 3: 24-bit
	int m_iStride;			//the stride for scan lines
	BOOL m_bTopDown;		//TRUE, the scan lines are top-down; 
							//FALSE, they are bottom up as DIB of Windows 

	BYTE * GetPixel(int x, int y)
	{
		if (x<0 || x>=m_iWidth || y<0 || y>=m_iHeight)
		{
			return NULL;
		}
		return m_paLineBuffer[y] + x * m_iPixelBytes;
	}

// Functions
public:
 //   UINT uiArea;

    CFlatImage()
    {
        m_iPixelBytes = 3;    // 24-bit
		m_iColorSequence = 0;
		m_iWidth = 0;
		m_iHeight = 0;
		m_iRedOffset = 0;
		m_iGreenOffset = 0;
		m_iBlueOffset= 0;
		m_iStride = 0;
		m_bTopDown = TRUE;
		m_iImageWidth = m_iImageHeight = 0;
		m_pbImage = NULL;
    }

    ~CFlatImage()
    {
    }

    BOOL CreateImage(	UINT uiW,				//Image width
						UINT uiH,				//Image height
						BYTE * pbImage,			//the buffer of image
						BOOL bTopDown,			//TRUE, the scan line is top-down;
												//FALSE, it is bottom-up as DIB
						int iBytesPerPixel,		//bytes per pixel, 3 or 4
						int iStride,			//stride in byte for every scan line, 0 for DWORD alignment
						int	iColorSequence,		//0 for RGB, 1 for BGR
						LPRECT	pRectImage		//pointer to a rectangle that specifies the range that CFlatImage associates with. NULL for entire image
					)
    {
		BOOL r = TRUE;
		RECT rImage;
		m_iColorSequence = iColorSequence;

		//DWORD alignment
		if (iStride == 0)
		{
			iStride = iBytesPerPixel * uiW;
			if (iStride % 4)
			{
				iStride = ((iStride / 4 ) + 1) * 4;
			}
		}

		//check for invalid parameters
		if ( (int) !uiW || (int) !uiH || !iBytesPerPixel || (int) uiW * iBytesPerPixel > iStride)
		{
			r = FALSE;
		}
		if (r && (!pbImage || IsBadReadPtr( pbImage, iStride * uiH )))
		{
			r = FALSE;
		}
		if (r && (iColorSequence != 0 && iColorSequence != 1))
		{
			r = FALSE;
		}

		if (r && pRectImage)
		{
			if (pRectImage->left < 0 || pRectImage->top < 0 
				|| pRectImage->right > (long) uiW || pRectImage->bottom > (long) uiH
				|| pRectImage->right <= pRectImage->left
				|| pRectImage->bottom <= pRectImage->top)
			{
				r = FALSE;
			}
			else
			{
				memcpy( &rImage, pRectImage, sizeof( RECT));
			}
		}
		else
		{
			rImage.left = rImage.top = 0;
			rImage.right = uiW;
			rImage.bottom = uiH;
		}
        
		if (r)
		{
			m_iImageWidth = uiW;
			m_iImageHeight = uiH;

			m_pbImage = pbImage;

			m_iWidth = rImage.right - rImage.left;
			m_iHeight = rImage.bottom - rImage.top;

			m_iPixelBytes = iBytesPerPixel;

			//Set the start pointers for each scan line
			m_paLineBuffer.clear();

			int i;
			if (bTopDown)
			{
				for (i=0;i<m_iHeight;i++)
				{
					m_paLineBuffer.push_back( m_pbImage + iStride * (rImage.top+i) + m_iPixelBytes * rImage.left );				
				}
			}
			else
			{
				for (i=0;i<m_iHeight;i++)
				{
					m_paLineBuffer.push_back( m_pbImage + iStride * (m_iImageHeight - 1 - rImage.top - i) + m_iPixelBytes * rImage.left );
				}
			}
			
			//Set color sequence
			if (m_iPixelBytes == 1)
			{
				m_iRedOffset = 0;
				m_iGreenOffset = 0;
				m_iBlueOffset = 0;
			}
			else if (iColorSequence)
			{
				m_iRedOffset = 2;
				m_iGreenOffset = 1;
				m_iBlueOffset = 0;
			}
			else
			{
				m_iRedOffset = 2;
				m_iGreenOffset = 1;
				m_iBlueOffset = 0;
			}
       
			m_iStride = iStride;
			m_bTopDown = bTopDown;
		}
        
		return r;
    }

	//Get red layer
	BYTE * GetRLayer(int x, int y)
	{
		BYTE * buf = GetPixel(x, y);
		if (buf)
		{
			buf += m_iRedOffset;
		}
		return buf;
	}

	//Get green layer
	BYTE * GetGLayer(int x, int y)
	{
		BYTE * buf = GetPixel(x, y);
		if (buf)
		{
			buf += m_iGreenOffset;
		}
		return buf;
	}

	//Get blue layer
	BYTE * GetBLayer(int x, int y)
	{
		BYTE * buf = GetPixel(x, y);
		if (buf)
		{
			buf += m_iBlueOffset;
		}
		return buf;
	}

	inline int	Height() { return m_iHeight; }
	inline int	Width() { return m_iWidth; }
	inline int	GetBytesPerPixel() { return m_iPixelBytes; }
	inline int	GetStride() { return m_iStride; }
	inline BOOL IsTopDown() { return m_bTopDown; };

	inline int ImageWidth() { return m_iImageWidth; };
	inline int ImageHeight() { return m_iImageHeight; };
	inline BYTE* GetBuffer() { return m_pbImage;};
	inline int GetColorSequence() { return m_iColorSequence;};
	BYTE* CropRotatedRect( RECT rect, float fRotate);
};

//Free the memory after used.
inline BYTE* CFlatImage::CropRotatedRect( RECT rect, float fRotate)
{
	
    int iU, iV;
    double dCos, dSin, dDU, dDV;
	double dCx, dCy;  //Center of the rect *2
	long scaledCos, scaledSin, scaledDU, scaledDV;
	long scaledWSrc, scaledHSrc, scaledCx, scaledCy;
	const long scaleFactor=65536L, scaleHalf=65536L/2;
	int nWSrc, nHSrc, nWDst, nHDst;
	BYTE* pBuf;

	nWSrc = ImageWidth();
	nHSrc = ImageHeight();
	nWDst = rect.right-rect.left;
	nHDst = rect.bottom-rect.top;
	dCx = rect.right + rect.left;
	dCy = rect.bottom + rect.top;
	assert ( nWDst > 0 && nHDst>0 );
	pBuf=new BYTE[nWDst*nHDst];

	dCos = cos(-fRotate);
	dSin = sin(-fRotate);
	dDU = ( dCx - nWDst*dCos - nHDst*dSin ) /2;
	dDV = ( dCy + nWDst*dSin - nHDst*dCos ) /2;
	
	scaledWSrc = (long) nWSrc * scaleFactor;
	scaledHSrc = (long) nHSrc * scaleFactor;
	scaledCx = (long) dCx * scaleFactor;
	scaledCy = (long) dCy * scaleFactor;

	scaledCos  = (long) ( dCos * scaleFactor + 0.5 );
	scaledSin  = (long) ( dSin * scaleFactor + 0.5 );
	scaledDU   = (long) ( dDU  * scaleFactor + 0.5 );
	scaledDV   = (long) ( dDV  * scaleFactor + 0.5 );

	long vSrc, uSrc;
	BYTE *pDst;
	int nX,nY;
    BYTE r,g,b;

 	for ( iV=0; iV<nHDst; iV++ ) {	//For row
		uSrc = iV*scaledSin + scaledDU;
		vSrc = iV*scaledCos + scaledDV;
		
		pDst = pBuf + iV*nWDst;

		for ( iU=0; iU<nWDst; iU++ ) {	//For Col
			if ( uSrc < 0 || vSrc < 0 || uSrc>=scaledWSrc || vSrc>=scaledHSrc )
				*pDst++=255;
			else {
				nX=uSrc>>16;
				nY=vSrc>>16;
		        r = *GetRLayer(nX,nY);
				g = *GetGLayer(nX,nY);
				b = *GetBLayer(nX,nY);
				*pDst++ = (BYTE)(0.114 * r + 0.588 * g + 0.298 * b);
			}
			uSrc+=scaledCos;
			vSrc-=scaledSin;
		}
	}

	return pBuf;
}	

int ExtractFeature64D(CFlatImage *pImage, void *pFeature);
int ExtractFeature8x8(CFlatImage *pImage, void *pFeature);
int ExtractFeature8x8CM(CFlatImage *pImage, void *pFeature);
int ExtractFeature8x8CMColor(CFlatImage *pImage, void *pFeature);
int ExtractFeatureEDH(CFlatImage *pImage, void *pFeature);

#endif // _FLATIMAGE_H_
