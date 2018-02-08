# compare two evaluation results A and B to show the results A is correct but B is not correct
# example file format:
#0	great wall of china	0		-1	0	FalseNegative(WronglyRejected)	https://thechive.files.wordpress.com/2015/06/everything-always-looks-better-on-instagram-photos-2.jpg?w=600&h=600	DummyID	0	CannotTell	-
#3	great wall of china	0	mutianyu	0.7343361	0	FalseNegative(WrongLabel)	http://www.travelfreak.com/wp-content/uploads/2013/03/great-wall-travel-tag.jpg	DummyID	3	No	No
# line format:
# 0:ImageKey 1:GroundTruth 2:BadOrNot 3:Prediction 4:Confidence 5:ThresholdUsed 6:EvaluateResult 7:MUrl 8:DummyID 9:ImageKey 10:LabelsToShow 11:EvaluateResult
# parameters: EvaluationResultFileName, ColImageKey, ColGroundtruth, ColPrediction, ColConfidence, ColMurl, ColEvaluationResult(Yes/No/-), Filter(11, 10, 1-, 


#from numpy  import *
#import numpy as np
#import matplotlib
#import matplotlib.pyplot as plt
#import matplotlib.animation as animation
#from numpy.random import rand
#import os
#import math
#from datetime import datetime
import argparse

def filterResult(rltFile, keep=True, rltCol=11, rltFilter="Yes", confCol=4, confThresh=-1.00):
    #rlt = open(logfile, 'r')
    rltFiltered = []
    try:
        for line in rltFile:
            cols = line.split('\t')
            if keep:
                if cols[rltCol].strip() == rltFilter and float(cols[confCol]) >= confThresh:
                    rltFiltered.append(line)
            else: #remove the (results = rltFilter and Conf > confThresh), i.e not-Yes results
                if cols[rltCol].strip() != rltFilter or float(cols[confCol]) < confThresh:
                    rltFiltered.append(line)
    except:
        print 'Error when reading file ' + logfile
    return rltFiltered

def main(argv):
    parser = argparse.ArgumentParser(description='Filter Results.')
    parser.add_argument('rlt_file',  type=open)
    parser.add_argument('out_file',  type=argparse.FileType('w'))
    parser.add_argument('-keepORremove', default='keep', choices=['keep', 'remove'])
    parser.add_argument('-rltCol', default=11, type=int)
    parser.add_argument('-rltFilter', default='Yes')
    parser.add_argument('-confCol', default=4, type=int)
    parser.add_argument('-confThresh', default=-1.0, type=float)
    cmd = parser.parse_args()
    #import sys
    filteredResult = filterResult(cmd.rlt_file, cmd.keepORremove=='keep', cmd.rltCol, cmd.rltFilter, cmd.confCol, cmd.confThresh) 
    cmd.out_file.writelines(filteredResult)
if __name__ == '__main__':
    import sys
    main(sys.argv)
    
    

