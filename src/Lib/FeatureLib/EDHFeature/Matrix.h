//-----------------------------------------------------------------------------
// MSR China Development
//
// Microsoft Confidential.  All Rights Reserved.
//
// File: Matrix.h - the definition of Class CMatrix
// 
// Owner:	Yanfeng Sun, yfsun@microsoft.com
//
// Last Modified: March 6, 2002 by Yanfeng Sun
//-----------------------------------------------------------------------------
#ifndef _MATRIX_H_INCLUDED
#define _MATRIX_H_INCLUDED


//the definition of class CFloatMatrix
class CFloatMatrix  
{
public:
	CFloatMatrix();
	~CFloatMatrix();

	HRESULT Create(const int nWidth, const int nHeight);

	int		Width() { return m_nWidth;};
	int		Height() { return m_nHeight;};

	float *	GetLineBuffer( const int iLine);
private:
	//data member
	int		m_nWidth;
	int		m_nHeight;
	float * m_pLineData;	//the data buffer
	float** m_pMatrix;		//the pointers array for the start pos of each row
};



#endif // _MATRIX_H_INCLUDED
