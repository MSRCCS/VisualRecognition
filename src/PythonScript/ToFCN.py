import numpy as np
import sys
import os
from os import path
import argparse
import math

def ConvertToFullConvNet(args):
    # Load the original network and extract the fully connected layers' parameters.
    net = caffe.Net(args.net0, args.model0, caffe.TEST)
    params = net.params.keys()[-3:]
    print 'layers that will be converted: {}'.format(params)
    # fc_params = {name: (weights, biases)}
    fc_params = {pr: (net.params[pr][0].data, net.params[pr][1].data) for pr in params}

    for fc in params:
        print '{} weights are {} dimensional and biases are {} dimensional'.format(fc, fc_params[fc][0].shape, fc_params[fc][1].shape)

    # Load the fully convolutional network to transplant the parameters.
    net_full_conv = caffe.Net(args.net1, args.model0, caffe.TEST)
    params_full_conv = net_full_conv.params.keys()[-3:]
    # conv_params = {name: (weights, biases)}
    conv_params = {pr: (net_full_conv.params[pr][0].data, net_full_conv.params[pr][1].data) for pr in params_full_conv}

    for conv in params_full_conv:
        print '{} weights are {} dimensional and biases are {} dimensional'.format(conv, conv_params[conv][0].shape, conv_params[conv][1].shape)

    for pr, pr_conv in zip(params, params_full_conv):
        conv_params[pr_conv][0].flat = fc_params[pr][0].flat  # flat unrolls the arrays
        conv_params[pr_conv][1][...] = fc_params[pr][1]

    net_full_conv.save(args.model1)

    #-pycaffe \\tnr-csgpu011\c$\users\yuxhu\src\caffe\python -net0 F:\fromS\data\Pokemon-v2-SlidingWindow\pokemon.v2.prototxt -model0 F:\fromS\data\Pokemon-v2-SlidingWindow\pokemon.v2.caffemodel -net1 F:\fromS\data\Pokemon-v2-SlidingWindow\pokemon.v2.fullconv.try.prototxt -model1 F:\fromS\data\Pokemon-v2-SlidingWindow\pokemon.v2.fullconv.try.caffemodel
if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument('-pycaffe', required=True, help='python caffe root dir')
    parser.add_argument('-net0', required=True, help='old prototxt file')
    parser.add_argument('-model0', required=True, help='old caffemodel file')
    parser.add_argument('-net1', required=True, help='new prototxt file')
    parser.add_argument('-model1', required=True, help='new caffemodel file')
    args = parser.parse_args()

    sys.path.insert(0, args.pycaffe)
    import caffe

    ConvertToFullConvNet(args)
    print('done.')
