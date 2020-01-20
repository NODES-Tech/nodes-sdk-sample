dotnet publish
copy ConsoleApplication\bin\Debug\netcoreapp3.0\publish\*.* publish
copy ConsoleApplication\bin\Debug\publish\*.* publish
echo  git commit -a --all -m "publish"
echo  git push
