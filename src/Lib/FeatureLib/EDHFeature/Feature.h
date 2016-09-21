//-----------------------------------------------------------------------------
// MSR China Development
//
// Microsoft Confidential.  All Rights Reserved.
//
// File: CFeature.h - the definition of Class CFeature
// 
// Owner: Yanfeng Sun, yfsun@microsoft.com
//		  Chunhui Hu
//
// Last Modified: March 6, 2002 by Yanfeng Sun
// Last Modified: April 26, 2002 by Mingjing Li
//
//-----------------------------------------------------------------------------

#ifndef _FEATURE_H_INCLUDED
#define _FEATURE_H_INCLUDED

#include "FeatureType.h"

//
//the defination for the length of each type of feather in float
//
#define LENGTH_ColorMoment12		6
#define	LENGTH_EdgeDirHistogram		13
#define	LENGTH_KirshDirDensity		16
#define	LENGTH_ColorTextureMoment	14

//
//defination for CFeature
//
class CFeature  
{
public:
	//
	//constructor and deconstructor
	//
	CFeature();
	virtual ~CFeature();

	//
	//public functions
	//
	HRESULT		Init(const IMAGE_FEATURE_TYPE enumThisType);
	float *		GetFeatureBuffer() { return m_pData; };
	
	IMAGE_FEATURE_TYPE GetType() { return m_enumType; };

	//
	//static functions
	//
	static UINT QueryFeatureLength(const IMAGE_FEATURE_TYPE emumWantedFeatureType);

private:
	//
	//data members
	//
	float *m_pData;			//feature buffer
	UINT m_nFeatureLength;	//the featue length is not always equal to the buffer length
	UINT m_nBufferLength;	//the bufferlength is equal to feature length or max length
	IMAGE_FEATURE_TYPE m_enumType;	//feature type
};

#endif 
