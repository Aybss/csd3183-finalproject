#pragma once
#include <vector>
#include <stdexcept>

template <typename T>
class MapLayer {
private:
    int width = 0;
    int height = 0;
    std::vector<T> data;

public:
    MapLayer() = default;
    MapLayer(int w, int h, const T& default_value = T()) 
        : width(w), height(h), data(w * h, default_value) {}

    void resize(int w, int h, const T& default_value = T()) {
        width = w;
        height = h;
        data.assign(w * h, default_value);
    }

    // Direct linear indexing access
    inline T& operator[](int index) { return data[index]; }
    inline const T& operator[](int index) const { return data[index]; }

    // Safe 2D coordinate access
    inline T& at(int row, int col) {
        if (row < 0 || row >= height || col < 0 || col >= width) {
            throw std::out_of_range("Grid coordinates out of bounds.");
        }
        return data[row * width + col];
    }

    inline const T& at(int row, int col) const {
        if (row < 0 || row >= height || col < 0 || col >= width) {
            throw std::out_of_range("Grid coordinates out of bounds.");
        }
        return data[row * width + col];
    }

    // Direct pointer access for Member 4's P/Invoke needs
    T* get_raw_ptr() { return data.data(); }
    const T* get_raw_ptr() const { return data.data(); }

    int get_width() const { return width; }
    int get_height() const { return height; }
};