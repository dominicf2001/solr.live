{ pkgs, lib, config, inputs, ... }:

{
  packages = with pkgs; [ bun ];

  languages.dotnet = {
    enable = true;
    package = pkgs.dotnet-sdk_8;
  };

  languages.typescript.enable = true;
}
