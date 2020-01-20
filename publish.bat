dotnet publish
copy -r ConsoleApplication/bin/Debug/publish/* ./publish/
git commit -a --all -m "publish"
git push
