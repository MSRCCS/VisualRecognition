# this tool replace a string in a file with another string
# params: infile: text file name 
# params: oldstr: the string to be found
# params: newstr: the string used to replace the old string

import argparse
import os.path
import shutil
import glob

def modifyFile(infile, oldstr, newstr):
    # Read in the file
    filedata = None
    with open(infile, 'r') as file :
      filedata = file.read()

    # Replace the target string
    count = filedata.count(oldstr)
    filedata = filedata.replace(oldstr, newstr)

    # Write the file out again
    with open(infile, 'w') as file:
      file.write(filedata)
    return count

def main(argv):
    parser = argparse.ArgumentParser(description='deploy a trained caffe model, e.g. DeployCaffeModel.py -source_folder C:\data\LandmarkV4.3 -out_folder F:\fromC\src\EntityRecognition\env\model\LandmarkV4.3')
    parser.add_argument('-infile')
    parser.add_argument('-oldstr')
    parser.add_argument('-newstr')
    
    cmd = parser.parse_args()
    print os.getcwd()
    assert os.path.isfile(cmd.infile), 'File does not exit: ' + cmd.infile
    assert len(str.strip(cmd.oldstr)) > 0, 'oldstr cannot be empty'
    shutil.copyfile(cmd.infile, cmd.infile + ".bak")
    count = modifyFile(cmd.infile, cmd.oldstr, cmd.newstr) 
    
    print 'replaced %d "%s" with "%s" in %s'%(count, cmd.oldstr, cmd.newstr, cmd.infile)
    
    
if __name__ == "__main__":
    import sys
    main(sys.argv)