#! /usr/bin/env python

import os
import os.path
import subprocess as subp
import threading
import time
import random

def bootstrap_file(task, semaphore):
    ct = threading.current_thread()
    semaphore.acquire()
    print("Running \"%s\"" % " ".join(task["command"]))
    with open(task["output"], "w") as log_fp:
        subp.call(task["command"], stdout=log_fp, stderr=subp.STDOUT)
    semaphore.release()

num_workers = 24
boot_size = 10000

jar_path = "/home/ubuntu/Data/incanter-processing/target/incanter-processing-0.1.0-SNAPSHOT-standalone.jar"

input_dir = "/home/ubuntu/Documents/doc-adc-oddities/dif_circle/data"
output_dir = os.getcwd()
base_command_list = ["java", "-Xms2G", "-Xmx2G", "-jar", jar_path, "-v", "2", "-b", "10", "-s", str(boot_size)]

input_files = ["data_circle_100000x100000_0.1.bin",
               "data_circle_100000x100000_0.2.bin",
               "data_circle_100000x100000_0.3.bin",
               "data_circle_100000x100000_0.4.bin",
               "data_circle_100000x100000_0.5.bin",
               "data_circle_100000x100000_0.6.bin",
               "data_circle_100000x100000_0.7.bin",
               "data_circle_100000x100000_0.8.bin",
               "data_circle_100000x100000_0.9.bin",
               "data_circle_100000x100000_1.bin",
               "data_circle_100000x100000_0.1-1.bin",
               "data_circle_100000x100000_0.2-1.bin",
               "data_circle_100000x100000_0.3-1.bin",
               "data_circle_100000x100000_0.4-1.bin",
               "data_circle_100000x100000_0.5-1.bin",
               "data_circle_100000x100000_0.6-1.bin",
               "data_circle_100000x100000_0.7-1.bin",
               "data_circle_100000x100000_0.8-1.bin",
               "data_circle_100000x100000_0.9-1.bin",
               "data_circle_100000x100000_1-1.bin"]

#input_files = [ "data_circle_100000x100000_1.bin",
#               "data_circle_100000x100000_0.1-1.bin"]

output_files = dict(zip(input_files, ["".join([f[0:-4], "-bootstrap" , ".txt"]) for f in input_files]))


sem = threading.Semaphore(num_workers)

all_t = []

print("Starting working threads...")

for f in input_files:
    input_path = os.path.join(input_dir, f)
    output_path = os.path.join(output_dir, output_files[f])

    new_task = {"output": output_path,
		"command": base_command_list + ["-f", input_path]}

    new_thread = threading.Thread(target=bootstrap_file, args=(new_task, sem))
    new_thread.start()
    all_t.append(new_thread)

print("Threads started. Waiting for them to finish...")
for t in all_t:
    t.join()

print("Done! Bye...")








