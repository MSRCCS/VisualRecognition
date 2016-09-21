//-----------------------------------------------------------------------------
// MSR China Development
//
// Microsoft Confidential.  All Rights Reserved.
//
// File: CFeature.cpp - the implementation of Class CFeature
// 
// Owner:	Yanfeng Sun, yfsun@microsoft.com
//			Chunhui Hu
//
// Last Modified: March 6, 2002 by Yanfeng Sun
// Last Modified: April 26, 2002 by Mingjing Li
//
//-----------------------------------------------------------------------------

#include "stdafx.h"
#include "Feature.h"

//===========================================================================
//Constructor
//===========================================================================
CFeature::CFeature()
{
	m_pData = NULL;
	m_nFeatureLength = 0;
	m_nBufferLength = 0;
}

//===========================================================================
//Destructor
//===========================================================================
CFeature::~CFeature()
{
	if(m_pData) 
	{
		delete[] m_pData;
		m_pData = NULL;
	}
}


//===========================================================================
//Init
//	Initialize the feature
//
//Parameters
//	enumThisType
//		specifies the type of feature to be created
//
///Return Values
//	S_OK indicates success. Otherwise for failure
//===========================================================================
HRESULT CFeature::Init(const IMAGE_FEATURE_TYPE enumThisType)//, bool bIsMaxAlloc)
{
	HRESULT hr = S_OK;

	//release the buffer if it was allocated before
	if(m_pData) 
	{
		delete[] m_pData;
		m_pData = NULL;
	}

	//Get the feature length
	m_nFeatureLength = QueryFeatureLength(enumThisType);

	//Set the feature type
	m_enumType = enumThisType;

	//Set the buffer length
	m_nBufferLength = m_nFeatureLength;


	if(m_nBufferLength != 0)
	{
		m_pData = new float[m_nBufferLength];
		if (!m_pData) 
		{
			hr = E_OUTOFMEMORY;
		}
		else
		{
			ZeroMemory(m_pData, m_nBufferLength*sizeof(float));
		}
	}
	return hr;
}

//===========================================================================
//QueryFeatureLength
//	a static function to return the length of the specified feature
//
//Parameters
//	enumWantedFeatureType
//		specifies the type of feature to be queried
//
///Return Values
//	The length of the feature. 0, failed
//===========================================================================
UINT CFeature::QueryFeatureLength(const IMAGE_FEATURE_TYPE enumWantedFeatureType)
{
	switch(enumWantedFeatureType)
	{
	case FTYPE_ColorLuvMoment12:
		return LENGTH_ColorMoment12;
	case FTYPE_EdgeDirHistogram:
		return LENGTH_EdgeDirHistogram;
	case FTYPE_KirshDirDensity:
		return LENGTH_KirshDirDensity;
	case FTYPE_ColorTextureMoment:
		return LENGTH_ColorTextureMoment;
	}
	return 0;
};
