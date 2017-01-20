# this tool deploy a trained caffe model to a folder, ready for evaluation (using EvaluationTool), or demo (using CaffeHost)
# params: sourcefolder: contains a Models folder (will choose the latest model file in it), train_val.prototxt, x.labelmap, mean.binaryproto, (optional: entity_info.tsv)
# params: destfolder: will create the modelcfg.txt, modelcfg.demo.txt, and coresponding .prototxt files
# params: (optional) model file to choose
# detail process: 
# 1. check/copy the source folder for requried data files and copy them to source folder
#   1. the latest or designated .caffemmodel file from sourceDir/Models
#   2. train_val.prototxt
#   3. imagenet_mean.binaryproto
#   4. entity_info.tsv
# 2. modify x.prototxt file:
#           1. replace the first two layers with 
#                input: "data"
#                input_dim: 1   //256 for batch test
#                input_dim: 3
#                input_dim: 227
#                input_dim: 227
#           2. remove last two layers: accuracy layer and loss layer
#           3. add prob layer: 
#                layer {
#                  name: "prob"
#                  type: "Softmax"
#                  bottom: "fc8"
#                  top: "prob"
#                }
#           4. (optinal) make sure the innner product layer num_output value is equal to the number of classes (# of lines in labelmap file)
# 3. construct the modelcfg.txt file 
#           proto: train_val.prototxt
#           model: caffenet_train_iter_200000.caffemodel
#           mean: imagenet_mean.binaryproto
#           labelmap: landmarkV3.labelmap
#           entityinfo: entity_info.tsv, get the entity list
#todo: 

import argparse
import os.path
import shutil
import glob

def modifyProtoFile(sourceFile, destFile, classNum,  batchSize = 1):
    f1 = open(sourceFile, 'r')
    f2 = open(destFile, 'w')
    for line in f1:
        l1 = line.replace('ClassNumber', str(classNum))
        l2 = l1.replace('BatchSize', str(batchSize))
        f2.write(l2)
    f1.close()
    f2.close()
    return

def main(argv):
    parser = argparse.ArgumentParser(description='deploy a trained caffe model, e.g. DeployCaffeModel.py -source_folder C:\data\LandmarkV4.3 -out_folder F:\fromC\src\EntityRecognition\env\model\LandmarkV4.3')
    parser.add_argument('-source_folder', default = os.getcwd())
    parser.add_argument('-proto_file', default = '\\\\ccs-z840-01\\deployTemplate\\caffenet.prototxt')
    parser.add_argument('-mean_file', default = 'imagenet_mean.binaryproto')
    parser.add_argument('-labelmap_file', default = '*.labelmap')
    parser.add_argument('-entityinfo_file', default = '\\\\ccs-z840-01\\deployTemplate\\entity_info.tsv')
    parser.add_argument('-modelconfig_file', default = '\\\\ccs-z840-01\\deployTemplate\\')
    
    parser.add_argument('-out_folder', required=True)
    parser.add_argument('-forSingleOrBatchTest', default='single', choices=['single', 'batch'])
    parser.add_argument('-modelNumber', default=100000, type=int)

    parser.add_argument('-template_folder', default = "\\\\ccs-z840-01\\deployTemplate")

    cmd = parser.parse_args()
    # verify model is ready
    modelFilePath = os.path.join(cmd.source_folder , "models\caffenet_train_iter_" + str(cmd.modelNumber) + ".caffemodel")
    protoFilePath = os.path.join(cmd.source_folder, cmd.proto_file)
    meanFilePath =  os.path.join(cmd.source_folder, cmd.mean_file)
    entityinfoFilePath =  os.path.join(cmd.source_folder, cmd.entityinfo_file)
    labelmapFilePath = os.path.join(cmd.source_folder, cmd.labelmap_file)
    files = glob.glob(labelmapFilePath)
    if len(files) >= 1:
        labelmapFilePath = files[0]
        if len(files) > 1:
            print 'Find mutiple labelmap files, use the first one: ' + labelmapFilePath
    else:
        print 'Cannot find ' + labelmapFilePath 
    assert os.path.isfile(modelFilePath), 'File does not exit: ' + modelFilePath
    assert os.path.isfile(protoFilePath), 'File does not exit: ' + protoFilePath
    assert os.path.isfile(meanFilePath), 'File does not exit: ' + meanFilePath
    assert os.path.isfile(entityinfoFilePath), 'File does not exit: ' + entityinfoFilePath
    assert os.path.isfile(labelmapFilePath), 'File does not exit: ' + labelmapFilePath
    
    #copy the required files to dest folder, rename if necessary
    if not os.path.isdir(cmd.out_folder):
        os.makedirs(cmd.out_folder)
    #make sure dest folder is empty
    if os.listdir(cmd.out_folder):
        confirmOverwrite = raw_input('Destination folder ' + cmd.out_folder + ' is not empty, do you want to overwrite? (y/n)')
        import distutils.util
        if not distutils.util.strtobool(confirmOverwrite):
            return
    
    newModelFilePath = os.path.join(cmd.out_folder , "deepnet.caffemodel")
    newProtoFilePath = os.path.join(cmd.out_folder, "deepnet.prototxt.orgin")
    newMeanFilePath = os.path.join(cmd.out_folder, "mean.binaryproto")
    newEntityinfoFilePath = os.path.join(cmd.out_folder, "entity_info.tsv")
    newLabelmapFilePath = os.path.join(cmd.out_folder, "labelmap.txt")

    shutil.copyfile(modelFilePath, newModelFilePath)
    shutil.copyfile(protoFilePath, newProtoFilePath)
    shutil.copyfile(meanFilePath, newMeanFilePath)
    shutil.copyfile(entityinfoFilePath, newEntityinfoFilePath)
    shutil.copyfile(labelmapFilePath, newLabelmapFilePath)   

    #modify the prototxt file
    #get the # of classes 
    classNum = sum(1 for line in open(newLabelmapFilePath))
    modifyProtoFile(newProtoFilePath, os.path.join(cmd.out_folder, "deepnet.prototxt"), classNum, 1)
    modifyProtoFile(newProtoFilePath, os.path.join(cmd.out_folder, "deepnet.batch.prototxt"), classNum, 256)

    #(optional)create model config file, for batch testing (CaffeTool extract), or online demo (CaffeHost)
    shutil.copyfile(os.path.join (cmd.template_folder, "modelcfg.txt"), os.path.join(cmd.out_folder, "modelcfg.txt"))
    shutil.copyfile(os.path.join (cmd.template_folder, "modelcfg.demo.txt"), os.path.join(cmd.out_folder, "modelcfg.demo.txt"))
    
    #show example 
    print 'Done! Deployed ' + cmd.source_folder + ' ==> ' + cmd.out_folder 
    print 'Now you can use the model for extraction, demo,  or evaluation...'
    print 'Example#1: CaffeTool.exe extract -blob prob -m ' + os.path.join(cmd.out_folder, "modelcfg.txt") + ' -imageCol 3  -i inputImage.tsv -o output.image.prob.tsv'
    print 'Example#2: CaffeTool.exe extract -blob fc7 -proto ' + os.path.join(cmd.out_folder, "deepnet.batch.prototxt") + ' -model ' + newModelFilePath +' -mean ' + newMeanFilePath + ' -imageCol 5 -i inputImage.tsv -o outImage.fc7.tsv'
    print 'Example#3: CaffeHost.exe -model ' + os.path.join(cmd.out_folder, "modelcfg.demo.txt") + ' -recogname LandmarkV2 -thresh 0.0001'
    print 'Example#4: EntityRecognition.exe landmark -modeldir ' + cmd.out_folder + ' -nruns 1 -threads 2 -conf 0.0 -imgs f:\data\LandmarkEvaluation\images -o c:\data\LandmarkV3.3\evaluation\Landmark250-LandmarkV3.out.tsv -nResults 1'

if __name__ == "__main__":
    import sys
    main(sys.argv)