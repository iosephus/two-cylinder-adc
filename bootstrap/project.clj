(defproject incanter-processing "0.1.0-SNAPSHOT"
  :description "FIXME: write description"
  :url "http://example.com/FIXME"
  :license {:name "Eclipse Public License"
            :url "http://www.eclipse.org/legal/epl-v10.html"}
  :dependencies [[org.clojure/clojure "1.6.0"]
                 [org.clojure/clojure-contrib "1.2.0"]
                 [org.clojure/tools.cli "0.3.1"]
                 [incanter "1.5.4"]
                 [gloss "0.2.2"]
                 [congomongo "0.4.3"]
                 ]
  :main ^:skip-aot incanter-processing.core
  :target-path "target/%s"
  :profiles {:uberjar {:aot :all}}
  :jvm-opts  [ "-Xms3G" "-Xmx3G"]
  )
