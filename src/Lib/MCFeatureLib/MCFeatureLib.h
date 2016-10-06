// MCFeatureLib.h

#pragma once

#include "FeatureExtractor.h"

using namespace System;
using namespace System::Collections;
using namespace System::Runtime::InteropServices;
using namespace System::Drawing;
using namespace System::Drawing::Imaging;

namespace MCFeatureLib 
{
	public ref class MCFeatureExtractor
	{
	public:
		int static GetFeatureDim(String ^feaType)
		{
			int nDim = 0;

			if (String::Compare(feaType, "64D", true) == 0)
				nDim = 64;
			else if (String::Compare(feaType, "8x8", true) == 0)
				nDim = 64;
			else if (String::Compare(feaType, "8x8CM", true) == 0)
				nDim = 128;
			else if (String::Compare(feaType, "8x8CMColor", true) == 0)
				nDim = 384;
			else if (String::Compare(feaType, "8x8_EDH_WH", true) == 0)
				nDim = 78;  //64 + 13 + 1
			else if (String::Compare(feaType, "8x8_EDH2x2_WH", true) == 0)
				nDim = 117; //64 + 13 * 4 + 1
			else if (String::Compare(feaType, "64D_EDH2x2_WH", true) == 0)
				nDim = 117; //64 + 13 * 4 + 1
			else if (String::Compare(feaType, "64D_EDH2x2", true) == 0)
				nDim = 116; //64 + 13 * 4
			else if (String::Compare(feaType, "64D_EDH", true) == 0)
				nDim = 77; //64 + 13

			return nDim;
		}

		// feaType has to be: 64D, 8x8, 8x8CM;
		bool static ExtrFeature(String ^feaType, array<Byte> ^imgData, int width, int height, int iStride, int iBytesPerPixels, int iColorSequence, BOOL bTopDow, array<float> ^pFeatureBuff)
		{
			pin_ptr<Byte> pImageData = &imgData[0];
			pin_ptr<float> pFeature = &pFeatureBuff[0];
			int nFlag;
			if (feaType == "64D")
				nFlag = CFeatureExtractor::ExtrFeature64D(pImageData, width, height, iStride, iBytesPerPixels, iColorSequence, bTopDow, pFeature);
			else if (feaType == "8x8")
				nFlag = CFeatureExtractor::ExtrFeature8x8(pImageData, width, height, iStride, iBytesPerPixels, iColorSequence, bTopDow, pFeature);
			else if (feaType == "8x8CM")
				nFlag = CFeatureExtractor::ExtrFeature8x8CM(pImageData, width, height, iStride, iBytesPerPixels, iColorSequence, bTopDow, pFeature);
			else if (feaType == "8x8CMColor")
				nFlag = CFeatureExtractor::ExtrFeature8x8CMColor(pImageData, width, height, iStride, iBytesPerPixels, iColorSequence, bTopDow, pFeature);
			else if (feaType == "8x8_EDH_WH")
				nFlag = CFeatureExtractor::ExtrFeature8x8_EDH_WHRatio(pImageData, width, height, iStride, iBytesPerPixels, iColorSequence, bTopDow, pFeature);
			else if (feaType == "8x8_EDH2x2_WH")
				nFlag = CFeatureExtractor::ExtrFeature8x8_EDH2x2_WHRatio(pImageData, width, height, iStride, iBytesPerPixels, iColorSequence, bTopDow, pFeature);
			else if (feaType == "64D_EDH2x2_WH")
				nFlag = CFeatureExtractor::ExtrFeature64D_EDH2x2_WHRatio(pImageData, width, height, iStride, iBytesPerPixels, iColorSequence, bTopDow, pFeature);
			else if (feaType == "64D_EDH2x2")
				nFlag = CFeatureExtractor::ExtrFeature64D_EDH2x2(pImageData, width, height, iStride, iBytesPerPixels, iColorSequence, bTopDow, pFeature);
			else if (feaType == "64D_EDH")
				nFlag = CFeatureExtractor::ExtrFeature64D_EDH(pImageData, width, height, iStride, iBytesPerPixels, iColorSequence, bTopDow, pFeature);
			return (0 == nFlag);
		}

		bool static ExtrFeature(String ^feaType, Bitmap ^imgData, array<float> ^pFeatureBuff)
		{
			Drawing::Rectangle rc = Drawing::Rectangle(0, 0, imgData->Width, imgData->Height);
			BitmapData ^bmpData = imgData->LockBits(rc, ImageLockMode::ReadOnly, imgData->PixelFormat);

			array<Byte> ^bmpBuffer = gcnew array<Byte>(bmpData->Stride * imgData->Height);
			Marshal::Copy(bmpData->Scan0, bmpBuffer, 0, bmpData->Stride * bmpData->Height);

			PixelFormat pixelFormat = bmpData->PixelFormat;
			int biBitCount = 24;
			if (pixelFormat == PixelFormat::Format32bppArgb)
			{
				biBitCount = 32;
			}
			else if (pixelFormat == PixelFormat::Format24bppRgb)
			{
				biBitCount = 24;
			}
			else if (pixelFormat == PixelFormat::Format8bppIndexed )
			{
				biBitCount = 8;
			}
			else
			{
				throw gcnew Exception("Image format is not 8 or 24 bits");
			}
			
			return ExtrFeature(feaType, bmpBuffer, imgData->Width, imgData->Height, 
							   bmpData->Stride, biBitCount >> 3, 1, TRUE, pFeatureBuff);
		}

        bool static ExtrFeature(String ^feaType, String ^imageFile, array<float> ^pFeatureBuff)
		{
			Bitmap ^imgData = nullptr;
			try
			{
				imgData = gcnew Bitmap(imageFile);
			}
			catch (ArgumentException ^)
			{
				throw gcnew ArgumentException("Image can't be opened");
			}
			return ExtrFeature(feaType, imgData, pFeatureBuff);
		}
	};
}
