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
    parser.add_argument('-outLabelMap', required= False, type= argparse.FileType('w'), help='the labelmap, if asked to save')
    parser.add_argument('-inLabelMap', required= False, type= open, help ='the label map to use, if provided')
    parser.add_argument('-col', default=1, type=int,   help='the column number contains the detection label data')
    args = parser.parse_args()
    return args



#!python3
import os
import sys
import json
import argparse
import collections
import operator

    
if __name__ == '__main__':

    # parse arguments
    args = parse_args()
    filename = os.path.splitext(args.inTsv.name);
    if not args.outTsv:
        args.outTsv = open(filename[0] + '.ml' + filename[1], 'w')

    all_labels = dict()
    labelmap_provided = args.inLabelMap is not None
    if labelmap_provided:
        for line in args.inLabelMap:
            (label,id)=line.strip().split('\t')
            all_labels[label]=int(id)
        print 'loaded %d labels from %s'%(len(all_labels), args.inLabelMap.name)
    
    badLabels = 0
    for i, line in enumerate(args.inTsv):
        cols = [x.strip() for x in line.split("\t")]
        if len(cols)>args.col:
            labelField=cols[args.col]
            if len(labelField)<=2:
                badLabels+=1
                print 'bad label at line # [%d] = %s, skipped %d'%(i,labelField,badLabels)
                cols[args.col] = ''
            else:
                rects = json.loads(cols[args.col])
                labelset = set()
                idset = set()
                for rect in rects:
                    label = rect['class']
                    labelset.add(label)
                for label in labelset:
                    if labelmap_provided:
                        assert label in all_labels, 'error! cannot find label %s in the provided labelmap file %s !'%(label, args.inLabelMap.name)
                    elif label not in all_labels:
                        all_labels[label]=len(all_labels)
                    idset.add('%d'%all_labels[label])
                cols[args.col] = ';'.join(idset)
            
        newline = '\t'.join(cols)
        args.outTsv.write(newline + '\n')
        print 'processed %d lines\r'%i,
    
    print '\n# of labels:\t%d'%(len(all_labels))
    print '\n# of lines:\t%d'%(i+1)
    
    if args.outLabelMap:
        sorted_labels = sorted(all_labels.items(), key = operator.itemgetter(1))
        for label in sorted_labels:
            args.outLabelMap.write('%s\t%d\n'%(label[0],label[1]))
