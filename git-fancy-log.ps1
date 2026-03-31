param (
    $commitNumber = 10
)

git --no-pager log -n $commitNumber --all --graph --oneline