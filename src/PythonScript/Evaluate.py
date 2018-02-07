# this tool test a trained model with data, evaluate it against groundtruth, and show the curves
# params: model_folder: contains a deployed Models folder, you can use DeployCaffeModel.py to get a trained model deployed
# params: groundtruth_folder: contains the images and labels, there may be multiple groundtruth versions, 
# params: data_folder: contains the images 
# detail process: 
# 1. test the model by images in groundtruth dataset, i.e. data.tsv --> data.out.tsv
#    e.g.: d:\github\entityRecognition\bin\Debug\EntityRecognition.exe landmark -modeldir F:\fromC\src\EntityRecognition\env\model\landmarkV4.1  -nruns 1 -threads 2 -conf 0.0 -imgs "f:\data\LandmarkEvaluation\images"  -o c:\data\LandmarkV4.1\evaluation\Landmark250-LandmarkV4.out.tsv -nResults 1
# 2. evaluate the test results against the groundtruth labels to get Yes(correct)/No(incorrect)/-(rejected) , i.e. data.out.tsv-->data.out.evaluate.tsv
#    e.g.: d:\github\entityRecognition\bin\Debug\EntityRecognition.exe evaluatenew -resultFile C:\data\LandmarkV4.1\Evaluation\Landmark250-LandmarkV4.out.tsv -resultDataCol 1 -resultConfCol 2 -groundtruthFile F:\data\LandmarkEvaluation\landmark250.groundtruth.correct.5.tsv -groundtruthPositiveLabelCol 1  -conf 0.0  -matchmethod -1 -treatKeyAsNumber -outTSV C:\data\LandmarkV4.1\Evaluation\Landmark250-LandmarkV4.out.evaluate.byGT5.tsv
# 3. calculate ROC based on the evaluation result, i.e. data.out.evaluate.tsv-->data.out.evaluate.ROC.tsv
#    e.g.: d:\github\entityRecognition\bin\Debug\EntityRecognition.exe ROC -i  C:\data\LandmarkV3.3\Evaluation\Landmark250-LandmarkV3.out.evaluate.byGT5.tsv -l 11 -c 4 -o C:\data\LandmarkV3.3\Evaluation\Landmark250-LandmarkV3.out.evaluate.byGT5.ROC.tsv
# 4. draw the curves with baselines, if any

def parse_args():
    """Parse input arguments."""
    parser = argparse.ArgumentParser(description='landmark recognition evaluation')
    parser.add_argument('-truthdir', required=True, help='import groundtruth and baseline files')
    parser.add_argument('-testdata', default='', help='import test datasets (containing groundtruth samples)')
    parser.add_argument('-name', default="", required=False,   help='the name of the experiment')
    parser.add_argument('-modeldir', required=True, help='the model to be evaluated')
    parser.add_argument('-reportdir', default='', help='the folder for all output, default=modeldir')
    args = parser.parse_args()
    return args


import shlex, subprocess
def run_win_cmd(cmd):
    result = []
    args = shlex.split(cmd)
    process = subprocess.Popen(args,
                               shell=True,
                               stdout=subprocess.PIPE,
                               stderr=subprocess.PIPE)
    for line in process.stdout:
        result.append(line)
    errcode = process.returncode
    for line in result:
        print(line)
    if errcode is not None:
        raise Exception('cmd %s failed, see above for details', cmd)

#!python3
import os
import sys
import json
import argparse
import numpy as np;
from sklearn import metrics;
import matplotlib.pyplot as plt
import glob;

#load the detection results, organized by classes
def load_truths(filein):
    truthdict = dict()
    with open(filein, "r") as tsvin:
        for line in tsvin:
            cols = [x.strip() for x in line.split("\t")]
            if len(cols)<2:
                continue
            key = cols[0]
            labels = cols[1].split('|')
            if key not in truthdict:
                truthdict[key]=set(labels)
            else:
                truthdict[key].add(labels)
    return truthdict


def load_predicts(filein,truths):
    retdict = { key:('',-1) for key in truths}
    with open(filein, "r") as tsvin:
        for i, line in enumerate(tsvin, 1):
            cols = [x.strip() for x in line.split("\t")]
            if len(cols)<3:
                continue
            import re
            keys = re.findall(r'\b\d+\b', cols[0])[-1:]
            key = keys[0] if keys else i
            if not truths.has_key(key):
                continue
            label = cols[1]
            conf = cols[2]
            retdict[key] = (label, conf)
    return retdict

def load_roc(filein):
    conf=[]
    precision=[]
    recall=[]
    with open(filein, "r") as tsvin:
        for line in tsvin:
            cols = [x.strip() for x in line.split("\t")]
            if len(cols)<3:
                continue
            conf.append(float(cols[0]))
            recall.append(float(cols[1]))
            precision.append(float(cols[2]))            
    return (conf, precision, recall)

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
        
def splitpath (filepath) :
    (dir,fname) = os.path.split(filepath);
    (basename,ext) = os.path.splitext(fname);
    return (dir,basename,ext);
    
# python imgdet.py --cmap image.labelmap --gpu 0 --net synlogo.caffemodel  --prototxt test.prototxt  --image test.jpg

def load_baseline(truthdir) :
    file_pattern = truthdir+"/*.report";
    baseline_files = glob.glob(file_pattern)    
    baseline_files = sorted(baseline_files);
    truthdict = dict();
    for file in baseline_files:
        (truth_dir, expname, ext) = splitpath(file); 
        with open(file,"r") as fin:
            report_metrics = json.load(fin);
            truthdict[expname] = report_metrics;
    return truthdict;
    
if __name__ == '__main__':

    TsvImageToolPath = r'd:/github/entityRecognition/bin/Debug/EntityRecognition.exe'
    assert os.path.isfile(TsvImageToolPath), 'Cannot find TsvImage.exe from ' + TsvImageToolPath

    # parse arguments
    args = parse_args()
    truthsfilepattern = '/'.join([args.truthdir , "*.groundtruth.*.tsv"])
    truthsfile = max(glob.iglob(truthsfilepattern), key=os.path.getctime).replace('\\','/')
    truthsdata = args.testdata if args.testdata else '/'.join([args.truthdir , "images"])
    modeldir = args.modeldir
    reportdir = args.reportdir if args.reportdir else modeldir
    
    assert os.path.isfile(truthsfile), truthsfile + " is not found"
    assert os.path.isdir(truthsdata), truthsdata + " is not found"
    assert os.path.isdir(modeldir), modeldir + " is not found"
    if not os.path.isdir(reportdir):
        os.makedirs(reportdir)
        print " created " + reportdir

    #todo: move to the end
    baselines = load_baseline(args.truthdir);
    
    # step#1: test model with groudtruth data
    # d:\github\entityRecognition\bin\Debug\EntityRecognition.exe landmark -modeldir F:\fromC\src\EntityRecognition\env\model\landmarkV4.1  -nruns 1 -threads 2 -conf 0.0 -imgs "f:\data\LandmarkEvaluation\images"  -o c:\data\LandmarkV4.1\evaluation\Landmark250-LandmarkV4.out.tsv -nResults 1
    (truthdir, fbase, ext) = splitpath(truthsfile)
    outfile = '/'.join([reportdir, fbase + ".out" + ext])
    cmd = TsvImageToolPath + " landmark -modeldir " + modeldir + " -nruns 1 -threads 2 -conf 0.0 -nResults 1 -imgs " + truthsdata + " -o " + outfile 
    print 'Step#1: test the model [%s] by images [%s] in groundtruth dataset'%(modeldir, truthsdata)
    print cmd
    run_win_cmd(cmd)
    print 'Step#1: Done!\n\n\n'

    # step#2: evaluate the test results by groundtruth labels
    #  d:\github\entityRecognition\bin\Debug\EntityRecognition.exe evaluatenew -resultFile C:\data\LandmarkV4.1\Evaluation\Landmark250-LandmarkV4.out.tsv -resultDataCol 1 -resultConfCol 2 -groundtruthFile F:\data\LandmarkEvaluation\landmark250.groundtruth.correct.5.tsv -groundtruthPositiveLabelCol 1  -conf 0.0  -matchmethod -1 -treatKeyAsNumber -outTSV C:\data\LandmarkV4.1\Evaluation\Landmark250-LandmarkV4.out.evaluate.byGT5.tsv
    (evaldir, fbase, ext) = splitpath(outfile)
    evalfile = '/'.join([reportdir, fbase + ".evaluate" + ext])
    cmd = TsvImageToolPath + " evaluatenew -resultFile " + outfile + " -resultDataCol 1 -resultConfCol 2 -groundtruthFile " + truthsfile + " -groundtruthPositiveLabelCol 1  -conf 0.0  -matchmethod -1 -treatKeyAsNumber -outTSV " + evalfile 
    print 'Step#2: evaluate out file [%s] by groundtruth file [%s] '%(outfile, truthsfile)
    print cmd
    run_win_cmd(cmd)
    print 'Step#2: Done!\n\n\n'

    # step#3 calculate ROC based on evalaute results
    # d:\github\entityRecognition\bin\Debug\EntityRecognition.exe ROC -i  C:\data\LandmarkV3.3\Evaluation\Landmark250-LandmarkV3.out.evaluate.byGT5.tsv -l 11 -c 4 -o C:\data\LandmarkV3.3\Evaluation\Landmark250-LandmarkV3.out.evaluate.byGT5.ROC.tsv
    (rocdir, fbase, ext) = splitpath(evalfile)
    rocfile = '/'.join([reportdir, fbase + ".roc" + ext])
    cmd = TsvImageToolPath + " ROC -i " + evalfile + " -l 11 -c 4 -o " + rocfile 
    print 'Step#3: generate ROC file [%s] according to evaluate result file [%s] '%(rocfile, evalfile)
    print cmd
    run_win_cmd(cmd)
    print 'Step#3: Done!\n\n\n'

    # step#4 draw the curves with baselines,
    exp_name = args.name if args.name else fbase;
    #exp_name = '%s_%g'%(exp_name,args.ovthresh);    
    report_name = exp_name if reportdir=='' else '/'.join([reportdir,exp_name]);
    report_fig = report_name + ".png";
    report_file = report_name + ".report" 
    dataset_name = os.path.basename(truthsfile);
    #load roc file
    (conf, precision, coverage) = load_roc(rocfile)
    with open(report_file,"w") as fout:
        report = {
            'conf' : conf,
            'precision' : precision,
            'coverage' : coverage
            }
        fout.write(json.dumps(report,indent=4, sort_keys=True));
    
    fig = plt.figure()
    plt.plot(coverage, precision,
             lw=2, label='%s' % (exp_name))
    for exp in sorted(baselines.keys()):
        precision = np.array(baselines[exp]['precision'])
        recall = np.array(baselines[exp]['coverage']) 
        plt.plot(recall, precision, lw=2, label='%s'%(exp))

    plt.xlim([0.0, 1.0])
    plt.ylim([0.0, 1.05])
    plt.xlabel('coverage')
    plt.ylabel('Precision')
    plt.title('Object predictection PR Curve on %s dataset'%dataset_name)
    plt.legend(loc="lower right")
    fig.savefig(report_fig,dpi=fig.dpi)
