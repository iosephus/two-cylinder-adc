#!/usr/bin/env python

import subprocess as subp

num_spins = 100000
num_shapes = 3

small_r = [0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0]
big_r = 1.0

num_from_small = [int(round(num_spins * x * x / (x * x + big_r * big_r))) for x in small_r]
num_from_big = [num_spins - x for x in num_from_small]

files_small_r = ["data_circle_100000x100000_0.1.bin",
                 "data_circle_100000x100000_0.2.bin",
                 "data_circle_100000x100000_0.3.bin",
                 "data_circle_100000x100000_0.4.bin",
                 "data_circle_100000x100000_0.5.bin",
                 "data_circle_100000x100000_0.6.bin",
                 "data_circle_100000x100000_0.7.bin",
                 "data_circle_100000x100000_0.8.bin",
                 "data_circle_100000x100000_0.9.bin",
                 "data_circle_100000x100000_1.bin"]

big_circle_file = "data_circle_100000x100000_1.bin"

for i, f in enumerate(files_small_r):
    output_file = f[0:-4] + "-1.bin"
    subp.call(["dd", "bs=%d" % (num_shapes * 8), "count=%d" % num_from_small[i], "if=%s" % f, "of=%s" % output_file])
    subp.call(["dd", "bs=%d" % (num_shapes * 8), "count=%d" % num_from_big[i], "if=%s" % big_circle_file, "of=%s" % output_file, "oflag=append", "conv=notrunc"])

