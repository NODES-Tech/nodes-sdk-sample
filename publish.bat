git checkout master
git pull
git checkout binaries
git merge master
dotnet publish
copy ConsoleApplication\bin\Debug\netcoreapp3.0\publish\*.* publish
copy ConsoleApplication\bin\Debug\publish\*.* publish
git commit -a --all -m "publish"
git push
