# VisualRecognition Project
This project is for research in visual recognition to experiment for data gathering, cleaning, large-scale processing, and visual recognition.

# Development
## Requirements
- VS 2013
- CUDA 7.5
- `git` and `git-lfs` for Windows
 - Make sure that `C:\Program Files\Git\cmd` is in the system path
 - If you haven't generated a ssh key pair (which is required to access private repositories), follow this guide: https://help.github.com/articles/generating-ssh-keys
 - `git-lfs` is required for for gitting binary files, e.g. model files and `caffe`'s 3rdparty library files. After installing Git LFS, make sure to run `git lfs init` to setup the global Git hooks necessary for Git LFS to work.

## Repository Setup
Run the following line in Powershell

    git clone --recursive git@github.com:MSRCCS/VisualRecognition.git ; git clone git@github.com:leizhangcn/wincaffe-3rdparty.git VisualRecognition/caffe/3rdparty
    
Then you should be able to build `VisualRecognition.sln` using Visual Studio.
