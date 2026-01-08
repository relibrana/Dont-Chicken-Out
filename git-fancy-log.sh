#!bin/bash

NUMBER=${1:-10}
git --no-pager log --oneline --graph --all -n $NUMBER
