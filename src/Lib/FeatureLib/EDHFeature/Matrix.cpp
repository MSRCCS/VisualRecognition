//-----------------------------------------------------------------------------
// MSR China Development
//
// Microsoft Confidential.  All Rights Reserved.
//
// File: Matrix.cpp - the implementation of Class CFloatMatrix
// 
// Owner: Yanfeng Sun	yfsun@microsoft.com
//
// Last Modified: March 6, 2002.
//-----------------------------------------------------------------------------

#include "stdafx.h"
#include "Matrix.h"

//////////////////////////////////////////////////////////////////////
// Construction/Destruction
//////////////////////////////////////////////////////////////////////
CFloatMatrix::CFloatMatrix()
{
	m_nWidth = 0;
	m_nHeight = 0;
	m_pLineData = NULL;
	m_pMatrix = NULL;
}

CFloatMatrix::~CFloatMatrix()
{
	if(m_pLineData != NULL)
	{
		delete[] m_pLineData;
		m_pLineData = NULL;
	}
	if(m_pMatrix != NULL) 
	{
		delete[] m_pMatrix;
		m_pMatrix = NULL;
	}
}

HRESULT CFloatMatrix::Create(const int nWidth, const int nHeight)
{
	HRESULT hr = E_FAIL;
	
	//make sure that not create two times.
	if(m_pLineData == NULL)
	{
		hr = S_OK;
	}


	if (hr == S_OK && nWidth >0 && nHeight >0 ) 
	{
		m_nWidth = nWidth;
		m_nHeight = nHeight;
		
		m_pLineData = new float[m_nWidth*m_nHeight];
		m_pMatrix = new float*[m_nHeight];
		
		if (m_pLineData && m_pMatrix)
		{
			m_pMatrix[0] = m_pLineData;
			for(int i=1; i<m_nHeight; i++) 
				m_pMatrix[i] = m_pMatrix[i-1] + m_nWidth;

			ZeroMemory(m_pLineData, m_nWidth*m_nHeight*sizeof(float));
			hr = S_OK;
		}
		else
		{
			hr = E_OUTOFMEMORY;
		}
	}
	else
	{
		hr = E_INVALIDARG;
	}
	//failed, release the memory
	if (hr != S_OK)
	{
		if(m_pLineData != NULL)
		{
			delete[] m_pLineData;
			m_pLineData = NULL;
		}
		if(m_pMatrix != NULL) 
		{
			delete[] m_pMatrix;
			m_pMatrix = NULL;
		}
	}
	return hr;
}

float * CFloatMatrix::GetLineBuffer( const int iLine)
{
	if (m_pLineData && m_pMatrix && iLine >= 0 && iLine < m_nHeight)
	{
		return m_pMatrix[iLine];
	}
	else
	{
		return NULL;
	}
}