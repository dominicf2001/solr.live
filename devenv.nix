{ pkgs, lib, config, inputs, ... }:

{
  packages = with pkgs; [ bun ];

  languages.dotnet = {
    enable = true;
    package = pkgs.dotnet-sdk_8;
  };

  processes = {
  	front.exec = "bun ./frontend/build/index.js";
  	back.exec = "cd ./backend; dotnet run";
  };

  languages.typescript.enable = true;
}
