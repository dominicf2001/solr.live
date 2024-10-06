{ pkgs, lib, config, inputs, ... }:

{
  packages = with pkgs; [ bun nodejs ];

  languages.dotnet = {
    enable = true;
    package = pkgs.dotnet-sdk_8;
  };

  env.PROTOCOL_HEADER="x-forwarded-proto";
  env.HOST_HEADER="x-forwarded-host";
  env.ORIGIN="https://test.dominicferrando.com";

  processes = {
  	front.exec = "bun ./frontend/build/index.js";
  	back.exec = "cd ./backend; dotnet run";
  };

  languages.typescript.enable = true;
}
