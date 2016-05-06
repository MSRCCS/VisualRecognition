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

import argparse
from os import path

def labelJudge(predict, label):
    SidConflation = {'sid':'sidcore'}
    SidConflation['cf02e309-97b8-9ee7-2a3b-40df707a4076'] ='b8a54187-a683-ac21-fa9d-1e3488f92b98';
    SidConflation['e8985400-1748-4094-a038-00718a194eba'] ='450b01bb-0500-4fda-bd2c-b78abe0b2ea4';

    if predict in SidConflation:
        predict = SidConflation[predict]
    if label in SidConflation:
        label = SidConflation[predict]

    if predict == label:
        judge = '1';
    else:
        judge = '-1';

    return judge;

def PredictionProcessing(args):
    if args.outtsv == "":
        args.outtsv = path.splitext(args.intsv)[0] + '.eval.tsv'

    predictionFile = args.intsv
    evaluationFile = args.outtsv
    labelCol = args.labelCol
    prediCol = args.predCol

    with open(predictionFile) as f:
        linesdeduped = sorted(set(line.strip('\n') for line in f))

    with open(evaluationFile, 'w') as f_output:
        for lines in linesdeduped:
            tokens = lines.split('\t')
            results = tokens[prediCol].split(';')[0].split(':');
            predicts = results[0];
            score = results[1];
            label = tokens[labelCol];

            judge = labelJudge(predicts, label);
            f_output.write(lines + '\t'+judge +'\t' + score +'\n');

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('-intsv', required=True, help = 'input model prediction result tsv file')
    parser.add_argument('-labelCol', required=True, type=int, help = 'label column index')
    parser.add_argument('-predCol', required=True, type=int, help = 'prediction column index')
    parser.add_argument('-outtsv', default="", help = 'output model prediction evaluation tsv file (default: replace tsv .ext with .eval.tsv')
    args = parser.parse_args()
    PredictionProcessing(args)

if __name__ == '__main__':
    main()