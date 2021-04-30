//
// Reference.cs
//
// Author:
//       fjy <jiyuan.feng@live.com>
//
// Copyright (c) 2020 fjy
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System.Collections.Generic;
using UnityEngine;

namespace libx {
    // 引用抽象
    public class Reference {
        // 引用的 Object, 没有用到(private)
        private List<Object> _requires;
        
        // Reference.IsUnused()
        public bool IsUnused() {

            // 下面的逻辑没有用到
            if (_requires != null) {
                for (var i = 0; i < _requires.Count; i++) {
                    var item = _requires[i];
                    if (item != null) {
                        continue;
                    }
                    Release();
                    _requires.RemoveAt(i);
                    // 索引不会变, 因为 RemoveAt
                    i--;
                }

                if (_requires.Count == 0)
                    _requires = null;
            }

            // 这个方法只用到 下面这个判断
            return refCount <= 0;
        }

        // 被引用的数量
        public int refCount;

        // 被引用加1
        // 只有 Reference 实现了 Retain
        // Reference.Retain()
        public virtual void Retain() {
            refCount++;
        }

        // 被引用减1
        // 只有 Reference 实现了 Release
        // Reference.Release()
        public virtual void Release() {
            refCount--;
        }

        // 没有用到 Reference.checkRequires
        private bool checkRequires {
            get {
                return _requires != null;
            }
        }

        // 没有用到 Reference.Require()
        public void Require(Object obj) {
            if (_requires == null)
                _requires = new List<Object>();

            _requires.Add(obj);
            Retain();
        }

        // 没有用到 Reference.Dequire()
        public void Dequire(Object obj) {
            if (_requires == null)
                return;

            if (_requires.Remove(obj))
                Release();
        }
    }
}
