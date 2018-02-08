# convert tsv data column for detection to tsv data for multi label recognition
# e.g. [{"diff": 0, "class": "chair", "rect": [262.0, 210.0, 323.0, 338.0]}, {"diff": 0, "class": "chair", "rect": [164.0, 263.0, 252.0, 371.0]}, {"diff": 1, "class": "chair", "rect": [4.0, 243.0, 66.0, 373.0]}, {"diff": 0, "class": "table", "rect": [240.0, 193.0, 294.0, 298.0]}]
# --> chair,table
# params: intsv: the input tsv 
# params: outtsv: the output tsv, with the label column replaced
# params: col: the column number contains the detection label data 

def parse_args():
    """Parse input arguments."""
    parser = argparse.ArgumentParser(description='convert tsv data column for detection to tsv data for multi label recognition')
    parser.add_argument('-inTsv', required=True, type=open, help='the input tsv containing detection label')
    parser.add_argument('-outTsv', required= False, type= argparse.FileType('w'), help='the output tsv, with the label column replaced')
    parser.add_argument('-col', default=1, type=int,   help='the column number contains the detection label data')
    parser.add_argument('-outLabelMap', required= False, type= argparse.FileType('w'), help='the labelmap, if asked to save')
    args = parser.parse_args()
    return args



#!python3
import os
import sys
import json
import argparse

    
if __name__ == '__main__':

    # parse arguments
    args = parse_args()
    filename = os.path.splitext(args.inTsv.name);
    if not args.outTsv:
        args.outTsv = open(filename[0] + '.ml' + filename[1], 'w')

    all_labels = set()

    for i, line in enumerate(args.inTsv):
        cols = [x.strip() for x in line.split("\t")]
        if len(cols)>args.col:
            rects = json.loads(cols[args.col]);
            labelset = set();
            for rect in rects:
                label = rect['class'];
                labelset.add(label)
            cols[args.col] = ','.join(labelset)
            all_labels.update(labelset)
        newline = '\t'.join(cols)
        args.outTsv.write(newline + '\n')
        print 'processed %d lines\r'%i,
    
    print '\n# of labels:\t%d'%(len(all_labels))
    print '\n# of lines:\t%d'%(i+1)
    
    if not args.outLabelMap:
        for i,label in all_labels:
            args.outLabelMap.write('%s\t%d\n'%(label,i))
