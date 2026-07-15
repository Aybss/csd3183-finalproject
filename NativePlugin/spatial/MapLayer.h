// spatial/MapLayer.h
#pragma once
#include <memory>
#include <stdexcept>

template <typename T>
class MapLayer {
private:
    int width = 0;
    int height = 0;
    std::unique_ptr<T[]> data; // Swapped std::vector for a direct array pointer

public:
    MapLayer() = default;

    MapLayer(int w, int h, const T& default_value = T())
        : width(w), height(h), data(std::make_unique<T[]>(w* h)) {
        for (int i = 0; i < w * h; ++i) {
            data[i] = default_value;
        }
    }

    void resize(int w, int h, const T& default_value = T()) {
        width = w;
        height = h;
        data = std::make_unique<T[]>(w * h);
        for (int i = 0; i < w * h; ++i) {
            data[i] = default_value;
        }
    }

    // This now works perfectly for ALL types, including bool!
    inline T& operator[](int index) { return data[index]; }
    inline const T& operator[](int index) const { return data[index]; }

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

    T* get_raw_ptr() { return data.get(); }
    const T* get_raw_ptr() const { return data.get(); }

    int get_width() const { return width; }
    int get_height() const { return height; }
};