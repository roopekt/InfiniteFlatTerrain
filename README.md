# Infinite Flat Terrain 

A real-time height map based terrain renderer with LOD, made using Unity.
![Close and far example of the terrain](https://github.com/roopekt/InfiniteFlatTerrain/blob/main/ReadmeData/close_and_far_example.png)

## Installation 

 1. Clone the repository
	```shell
	git clone https://github.com/roopekt/InfiniteFlatTerrain
	```
2. Install Visual Studio
	https://visualstudio.microsoft.com/downloads/
3. Install Unity
	- Install Unity Hub from https://unity3d.com/get-unity/download
	- Using the hub, install  an editor with the version 2020.3 LTS. Remember to check visual studio integration when prompted about additional packages.
4. Locate and open the project trough Unity Hub

## Usage 

 - Turn camera by dragging with mouse
 - Move with w, a, s, d, shift and space keys
 - adjust speed with the scroll wheel
 - close by pressing esc

## How it works 

1. The terrain is a big circular disk tiled in a simple manner
![Tiled disk](https://github.com/roopekt/InfiniteFlatTerrain/blob/main/ReadmeData/tiled_disk.png)
2. Vertex positions are periodically rendered into a multibuffered texture using a compute shader. Height is calculated using Perlin noise with multiple layers.
3. Normals are also generated by a compute shader.
4. A vertex shader reads the vertex positions and normals from the textures.
5. A fragment shader decides the color of each pixel based on height and slope.
6. Water is just a simple transparent plane.

## License 

This project is distributed under the MIT License. See `LICENSE.txt` for more information.

![Mountain with a lake behind it](https://github.com/roopekt/InfiniteFlatTerrain/blob/main/ReadmeData/mountain.png)