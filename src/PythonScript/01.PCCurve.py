#-------------------------------------------------------------------------------
# Name:        module1
# Purpose:
#
# Author:      yag
#
# Created:     14/04/2016
# Copyright:   (c) yag 2016
# Licence:     <your licence>
#-------------------------------------------------------------------------------

from numpy  import *
import numpy as np
import matplotlib.pyplot as plt
import matplotlib.animation as animation
from numpy.random import rand
from os import path
import argparse


def ROCCurve(result, style, labelIndex, predictIndex, weightIndex):
    lfwPairWiseResults = open(result, 'r');
    resultList= [];
    TotalLabelCnt = 0;
    for imagePairWiseResults in lfwPairWiseResults:
        results = imagePairWiseResults.split('\t')
        label = results[labelIndex]
        try:
            float(results[predictIndex])
            if weightIndex >=0:
                float(results[weightIndex])
            #predict = float(results[predictIndex])
            #weight = float(results[weightIndex])
        except:
            predict = 0;
            print(results);
            weight = 0;
        predict = float(results[predictIndex])
        if weightIndex >=0:
            weight = float(results[weightIndex])
        else:
            weight = 1.0;
        if predict != 9999.99:
            resultList.append((predict, label, weight))
        TotalLabelCnt = TotalLabelCnt + weight;

    resultList.sort(key = lambda x: x[0], reverse=True)

    total_num = len(resultList)
    #positives = len([x for x in resultList if x[1] == '1'])
    #negatives = len([x for x in resultList if x[1] == '-1'])

    tp = tn = fp = fn = 0
    threshold_delta = 1.0 / 50;
    threshold = 0.0
    tpRate = []
    fpRate = []
    predictSV = []
    count = 0
    bucket_max = float(total_num) / 50

    for result in resultList:
        predict = result[0]
        label = result[1]
        weight = result[2]
        if label == '1':
            tp += weight
        if label == '-1':
            fp += weight

        precision = float(tp) / float(tp+fp)
        if TotalLabelCnt > 0:
            recall = float(tp+fp) / TotalLabelCnt; #(total_num-1901)
        else:
            recall = float(tp) / 1; # Debug only

        count += 1
        if recall >= threshold or count >= bucket_max:
            fpRate.append(recall)
            tpRate.append(precision)
            predictSV.append(predict)
            threshold += threshold_delta
            count = 0

    figure, =plt.plot(fpRate, tpRate, style)
    return figure


def PCCurveGeneration(args):
    configFile = args.config

    with open(configFile) as f:
        linesdeduped = sorted(set(line.strip('\n') for line in f))
    curves = []
    legends = []

    for lines in linesdeduped:
        tokens = lines.split('\t')
        predictionEvaluation = tokens[0];
        legend = tokens[1];
        marks = tokens[2];
        judgeCol =int(tokens[3]);
        confiCol =int(tokens[4]);
        weightCol = int(tokens[5]);

        curves.append(ROCCurve(path.normpath(predictionEvaluation), marks, judgeCol, confiCol, weightCol))
        legends.append(legend)


    plt.legend(curves, legends, loc=4)
    plt.ylabel('precision')
    plt.xlabel('recall')
    plt.axis([0.0, 1.05, 0.0, 1.05])
    plt.grid(True)
    plt.show()
    color = 'blue';

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('-config', required=True, help = 'input configuration file')
    args = parser.parse_args()
    PCCurveGeneration(args)	

if __name__ == '__main__':
    main()
    main()