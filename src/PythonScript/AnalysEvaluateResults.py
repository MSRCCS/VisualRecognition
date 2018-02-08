# this tool analyze evaluation results for the misclassification reasons
# gt=groundtruth, pred=prediction, tr=training data, conf=confidence score, th=Confidence threshold
# 0.A. gt==pred && conf>=th : correct prediction
# 0.B. gt=="" && conf<th: correct reject a non-landmark
# 1. gt!="" && gt not in tr (==>gt!=pred): class not in training data --> improve coverage
# 2. gt!="" && gt in tr && gt==pred && conf<th: prediction label is correct, but confidence score is not high enough --> add more varationas to tr/improve classifier
# 3. gt!="" && gt in tr && gt!=pred && conf<th: wrong predict, but good to reject --> add more varations / check similar classes/ improve classifier
# 4. gt!="" && gt in tr && pred!="" && gt!=pred && conf>th: wrong label (groundtruth is A and in training data, recognized as B, with conf > threshold) --> check training data (same landmark, different class labels; landmark is general (e.g. forest, river, beach, etc.); landmark is very similar (e.g. miniature version, photos); improve classifier
# 5. gt!="" && gt in tr && pred=="" && conf>= th: wrongly rejected --> sample very close to non-landmark training data, check non-landmark classes 
# 6. gt=="" && pred!="" && conf>=th: false alarm (groundtruth is "None Landmark", recognized as a known landmark, with conf > threshold --> improve non-landmark classes
# 7. other: e.g. no prediction due to unaccessible image
# params: evaluation_file: contains evaluation result (including groundtruth, prediction, 
# params: model_folder: contains a deployed Models folder, you can use DeployCaffeModel.py to get a trained model deployed
# params: groundtruth_folder: contains the images and labels, there may be multiple groundtruth versions, 
# params: data_folder: contains the images 

import os
import sys
import argparse
import numpy as np
import matplotlib.pyplot as plt
import glob

def parse_args():
    """Parse input arguments."""
    parser = argparse.ArgumentParser(description='recognition result analysis')
    parser.add_argument('-entityinfofile', type=open, help='enity_info file containing training data class info, col#0: entityID; col#1:entity name label.')
    parser.add_argument('-evaluatefile', required=True, type=open, help='evaluate file containing groundtruth, prediction and confidence')
    parser.add_argument('-gtCol', default=1, type=int)
    parser.add_argument('-predCol', default=3, type=int)
    parser.add_argument('-confCol', default=4, type=int)
    parser.add_argument('-confThresh', default=0.53, type=float, help='confidence threshold for rejection')
    parser.add_argument('-out_file',  type=argparse.FileType('w'))
    
    args = parser.parse_args()
    return args


#load the detection results, organized by classes
def load_entityinfo(tsvin):
    entitydict = dict()
    for line in tsvin:
        cols = [x.strip() for x in line.split("\t")]
        if len(cols)<2:
            continue
        key = cols[0].lower()
        label = cols[1].lower()
        if key not in entitydict.keys():
            entitydict[key] = label
    return entitydict


def load_evaluate(tsvin, gtCol=1, predCol=2, confCol=3):
    resultlist = list()
    for line in tsvin:
        cols = [x.strip() for x in line.split("\t")]
        if len(cols)<4:
            continue
        gt = set(cols[gtCol].lower().strip().split('|'))
        pred = cols[predCol].lower().strip()
        conf = float(cols[confCol])
        resultlist.append((gt, pred, conf))
    return resultlist

def evaluate (predicts, truths):
    #calculate npos
    npos = 0;
    for key in truths:
        labels = truths[key]
        npos += 1 if not '' in labels else 0
    nd = len(predicts)
    y_trues = [];
    y_scores = [];
    for key in truths.keys():
        truth_labels = truths[key]
        pred = predicts[key]
        y_true = 0;
        if pred[0] in truth_labels:
            y_true=1;
        y_trues += [ y_true ];
        y_scores += [ float(pred[1]) ];
    return (np.array(y_scores),np.array(y_trues), npos);
    
if __name__ == '__main__':

    # parse arguments
    args = parse_args()

    # parse evaluate file and training class labels
    if args.entityinfofile:
        tr = set(load_entityinfo(args.entityinfofile).values())
        print 'loaded %d classes from %s'%(len(tr), args.entityinfofile.name)
        check_training_data = True
    else:
        check_training_data = False
        print 'no entity info file provided, will skip checking whether ground truth is in training data\n'
    
    rlts = load_evaluate(args.evaluatefile, args.gtCol, args.predCol, args.confCol)
    print 'loaded %d results from %s\n'%(len(rlts), os.path.abspath(args.evaluatefile.name))
    
    errs = list()
    for rlt in rlts:
        #print rlt[0] +  '\t' + rlt[1] + '\t' + str(rlt[2])
        (gt, pred, conf) = rlt
        gt_empty = (('' in gt) and len(gt)==1) 
        pred_empty = len(pred)==0
        gt_eq_pred = pred in gt
        conf_le_th = conf >= args.confThresh
        gt_in_tr = (not check_training_data) or (len( gt & tr) > 0)

# 0.A. gt==pred && conf>=th : correct prediction
# 0.B. gt=="" && conf<th: correct reject a non-landmark
# 1. gt!="" && gt not in tr (==>gt!=pred): class not in training data --> improve coverage
# 2. gt!="" && gt in tr && gt==pred && conf<th: prediction label is correct, but confidence score is not high enough --> add more varationas to tr/improve classifier
# 3. gt!="" && gt in tr && gt!=pred && conf<th: wrong predict, but good to reject --> add more varations / check similar classes/ improve classifier
# 4. gt!="" && gt in tr && pred!="" && gt!=pred && conf>=th: wrong label (groundtruth is A and in training data, recognized as B, with conf > threshold) --> check training data (same landmark, different class labels; landmark is general (e.g. forest, river, beach, etc.); landmark is very similar (e.g. miniature version, photos); improve classifier
# 5. gt!="" && gt in tr && pred=="" && conf>= th: wrongly rejected --> sample very close to non-landmark training data, check non-landmark classes 
# 6. gt=="" && pred!="" && conf>=th: false alarm (groundtruth is "None Landmark", recognized as a known landmark, with conf > threshold --> improve non-landmark classes
# 7. other: e.g. no prediction due to unaccessible image

        if gt_eq_pred and conf_le_th:
           ErrorType=0
        elif gt_empty and not conf_le_th:
           ErrorType=0
        elif not gt_empty and not gt_in_tr:
           ErrorType=1
        elif not gt_empty and gt_in_tr and gt_eq_pred and not conf_le_th:
           ErrorType=2
        elif not gt_empty and gt_in_tr and not gt_eq_pred and not conf_le_th: 
           ErrorType=3
        elif not gt_empty and gt_in_tr and not gt_eq_pred and not pred_empty and conf_le_th: 
           ErrorType=4
        elif not gt_empty and gt_in_tr and pred_empty and conf_le_th: 
           ErrorType=5
        elif gt_empty and not pred_empty and conf_le_th:
           ErrorType=6
        else: 
           ErrorType=7
        
        errs.append((gt,pred,conf,ErrorType))
        print '%s\t%s\t%f\t%d'%(gt,pred,conf,ErrorType)

    from collections import Counter
    c=Counter([x[3] for x in errs])
    print c
    
    if not args.out_file :
        outfile = os.path.splitext(args.evaluatefile.name)[0] + ".analysis.tsv"
        args.out_file = open(outfile, "w")

    for err in errs:
        args.out_file.write('%s\t%s\t%f\t%d\n'%('|'.join(err[0]), err[1],err[2],err[3]))
    
    ErrDesc = {0:"Correct", 1:"Not in Training", 2:"A->A, but conf<th", 3:"A->B|'', conf<th", 4:"A->B, conf>=th", 5:"A->'', conf>th", 6:"''->A, conf>th", 7:"other"}
    for item in c.items():
        args.out_file.write('#ErrorType:\t%s:\t%s\t%s\n'%(item[0],item[1], ErrDesc[item[0]]))
    args.out_file.close()

    OX = [ '[%d]:%s'%(x[0],ErrDesc[x[0]]) for x in c.items()]
    OY = [ x[1] for x in c.items()]
    #fig = plt.figure()
    fig, ax = plt.subplots()
    width = .35
    ind = np.arange(len(OY))
    plt.bar(ind, OY, width=width)
    for i, v in enumerate(OY):
        ax.text( i, v + 3 , str(v), color='blue', fontweight='bold')
    plt.xticks(ind + width / 2, OX)
    plt.title('Evaluation Result Summary: Total# of Samples: %d\n%s'%(len(rlts), args.evaluatefile.name))
    fig.autofmt_xdate()
    figurefile = os.path.splitext(args.out_file.name)[0] + ".png"
    print 'saving bar chart to %s'%(figurefile)
    plt.savefig(figurefile)    