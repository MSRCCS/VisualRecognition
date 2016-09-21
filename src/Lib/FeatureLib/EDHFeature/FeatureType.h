//-----------------------------------------------------------------------------
// MSR China Development
//
// Microsoft Confidential.  All Rights Reserved.
//
// File: FeatureType.h - the definiton of all feature types.
//
// Owner: Yanfeng Sun	(yfsun@microsoft.com)
//
// Last Modified: March 1, 2002
// Last Modified: April 26, 2002 by Mingjing Li
//
// Note:This file is a sub-set of the full feature extractor library developed by
//		Microsoft Research Asia.
//
//-----------------------------------------------------------------------------
#ifndef	_FEATURETYPE_H_INCLUDED
#define _FEATURETYPE_H_INCLUDED
//--------------------------------------
//	Type definition						
//--------------------------------------
//The definition of the image feature type.

typedef enum 
{
	//Color moments 
	FTYPE_ColorLuvMoment12			=	11,//length 6

	//Edge direction histogram
	FTYPE_EdgeDirHistogram			=	23,//length 13

    // Kirsh direction density
    FTYPE_KirshDirDensity           =   46,//length 16

    // Color Texture Moment
    FTYPE_ColorTextureMoment        =   44,//length 14

} IMAGE_FEATURE_TYPE;

#endif	//end of _FEATURETYPE_H_INCLUDED



